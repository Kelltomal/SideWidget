import {useEffect,useRef,useState} from 'react';
import {invoke} from './ui';
import {bridge} from './bridge';
import {toDictationWav} from './localDictation';

const mimeType='audio/webm;codecs=opus';
const recordingLimit=512*1024;
type Phase='idle'|'preparing'|'opening'|'recording'|'ready'|'translating'|'downloadingVoice'|'loadingAudio'|'playing';
type SpeechField='source'|'target';
type Session={release?:()=>void;recording?:Promise<Blob>;stop?:()=>void;request?:boolean;local?:boolean;preparing?:boolean;operation?:string};

function encode(blob:Blob){
 return new Promise<string>((resolve,reject)=>{
  const reader=new FileReader();
  reader.onerror=()=>reject(new Error('Не удалось прочитать запись. Запишите фразу заново.'));
  reader.onload=()=>resolve((reader.result as string).split(',')[1]);
  reader.readAsDataURL(blob);
 });
}

export function useSonioxVoice(active:boolean,source:string,target:string){
 const current=useRef<Session|null>(null);
 const [phase,setPhase]=useState<Phase>('idle');
 const [error,setError]=useState('');
 const [speakingField,setSpeakingField]=useState<SpeechField|null>(null);
 const [progress,setProgress]=useState<number|null>(null);
 const [voiceDownload,setVoiceDownload]=useState<{received:number;total:number}|null>(null);
 const [value,setValue]=useState<{transcript:string;text:string}>();

 function finish(session:Session,nextError=''){
  if(current.current!==session)return;
  current.current=null;
  session.release?.();
  setPhase('idle');
  setSpeakingField(null);
  setVoiceDownload(null);
  setError(nextError);
 }
 function cancel(reset=true){
  const session=current.current;
  if(!session)return;
  current.current=null;
  session.release?.();
  if(session.request)void invoke('translation.cancelSpeech').catch(()=>{});
  if(reset){setPhase('idle');setSpeakingField(null);setVoiceDownload(null);}
 }
 useEffect(()=>{
  if(!active){setPhase('idle');setSpeakingField(null);setVoiceDownload(null);}
  return()=>cancel(false);
 },[active]);

 useEffect(()=>bridge.subscribe(event=>{
  const session=current.current;
  if(event.type==='translation.local.progress'&&session?.preparing)
   setProgress(event.data.percent);
  if(event.type!=='translation.speech.progress'||!session?.operation||event.data?.operation!==session.operation)return;
  const {phase,received,total}=event.data;
  if(phase==='download'&&typeof received==='number'&&typeof total==='number'&&total>0){
   setVoiceDownload({received,total});
   setPhase('downloadingVoice');
  }
  if(phase==='preparing'||phase==='playing'){
   setVoiceDownload(null);
   setPhase(phase==='playing'?'playing':'loadingAudio');
  }
 }),[]);

 async function start(local=false){
  if(!active||current.current)return;
  const session:Session={local};
  current.current=session;
  setPhase('opening');
  setError('');
  try{
   if(!navigator.mediaDevices?.getUserMedia||!globalThis.MediaRecorder?.isTypeSupported(mimeType))
    throw new Error('Запись микрофона недоступна. Откройте переводчик в приложении SideWidget.');
   if(local){
    session.preparing=session.request=true;
    setProgress(null);
    setPhase('preparing');
    await invoke('translation.localMicrophone');
    session.preparing=session.request=false;
   }else await invoke('translation.microphone');
   if(current.current!==session)return;
   setPhase('opening');
   let stream:MediaStream;
   try{stream=await navigator.mediaDevices.getUserMedia({audio:{channelCount:1},video:false});}
   catch{throw new Error('Микрофон недоступен. Проверьте разрешения Windows и устройство ввода.');}
   const stopTracks=()=>stream.getTracks().forEach(track=>track.stop());
   if(current.current!==session){stopTracks();return;}
   session.release=stopTracks;
   const recorder=new MediaRecorder(stream,{mimeType,audioBitsPerSecond:32000});
   const chunks:Blob[]=[];
   let resolveRecording:(value:Blob)=>void;
   session.recording=new Promise(resolve=>{resolveRecording=resolve;});
   let limit:ReturnType<typeof setTimeout>;
   session.stop=()=>{
    clearTimeout(limit);
    if(recorder.state!=='inactive')recorder.stop();
    stopTracks();
   };
   session.release=session.stop;
   recorder.ondataavailable=event=>{if(event.data.size)chunks.push(event.data);};
   recorder.onstop=()=>{
    clearTimeout(limit);
    stopTracks();
    resolveRecording(new Blob(chunks,{type:mimeType}));
    if(current.current===session)setPhase(previous=>previous==='recording'?'ready':previous);
   };
   recorder.onerror=()=>{
    resolveRecording(new Blob());
    finish(session,'Не удалось записать звук. Проверьте микрофон и повторите.');
   };
   recorder.start();
   limit=setTimeout(session.stop,30000);
   setPhase('recording');
  }catch(e){
   finish(session,e instanceof Error?e.message:'Не удалось записать звук. Проверьте микрофон и повторите.');
  }
 }

 async function submit(onTranscript?: (transcript:string)=>void){
  const session=current.current;
  if(!session?.recording||!session.stop)return;
  const stop=session.stop;
  session.stop=undefined;
  setPhase('translating');
  try{
   stop();
   const blob=await session.recording;
   if(current.current!==session)return;
   if(!blob.size||blob.size>recordingLimit)throw new Error('Не удалось прочитать запись. Запишите фразу заново.');
   let recording=blob;
   if(session.local){
    try{recording=await toDictationWav(blob);}
    catch{throw new Error('Не удалось прочитать запись. Запишите фразу заново.');}
   }
   if(current.current!==session)return;
   const audio=await encode(recording);
   if(current.current!==session)return;
   session.request=true;
   if(onTranscript){
    const result=await invoke<{transcript:string}>(session.local?'translation.localDictate':'translation.dictate',{audio,source});
    if(current.current!==session)return;
    onTranscript(result.transcript);
   }else{
    const result=await invoke<{transcript:string;text:string}>('translation.voice',{audio,source,target});
    if(current.current!==session)return;
    setValue(result);
   }
   finish(session);
  }catch(e){
   finish(session,e instanceof Error?e.message:onTranscript?'Не удалось распознать речь.':'Не удалось выполнить перевод');
  }
 }

 async function speak(text:string,language:string,field:SpeechField){
  if(!active||!text.trim()||current.current)return;
  const operation=crypto.randomUUID();
  const session:Session={request:true,operation};
  current.current=session;
  setSpeakingField(field);
  setPhase('loadingAudio');
  setError('');
  try{
   await invoke('translation.speakLocal',{text,language,operation});
   finish(session);
  }catch(error){
   finish(session,error instanceof Error?error.message:'Не удалось воспроизвести текст.');
  }
 }

 return {phase,error,value,progress,voiceDownload,speakingField,start,submit,speak,cancel,clear:()=>{setValue(undefined);setError('');}};
}
