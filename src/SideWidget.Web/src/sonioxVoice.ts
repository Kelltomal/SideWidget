import {useEffect,useRef,useState} from 'react';
import {invoke} from './ui';

const mimeType='audio/webm;codecs=opus';
const recordingLimit=512*1024;
type Phase='idle'|'opening'|'recording'|'ready'|'translating'|'loadingAudio'|'playing';
type Session={release?:()=>void;recording?:Promise<Blob>;stop?:()=>void;request?:boolean};

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
 const [value,setValue]=useState<{transcript:string;text:string}>();

 function finish(session:Session,nextError=''){
  if(current.current!==session)return;
  current.current=null;
  session.release?.();
  setPhase('idle');
  setError(nextError);
 }
 function cancel(reset=true){
  const session=current.current;
  if(!session)return;
  current.current=null;
  session.release?.();
  if(session.request)void invoke('translation.cancelSoniox').catch(()=>{});
  if(reset)setPhase('idle');
 }
 useEffect(()=>{
  if(!active)setPhase('idle');
  return()=>cancel(false);
 },[active]);

 async function start(){
  if(!active||current.current)return;
  const session:Session={};
  current.current=session;
  setPhase('opening');
  setError('');
  try{
   if(!navigator.mediaDevices?.getUserMedia||!globalThis.MediaRecorder?.isTypeSupported(mimeType))
    throw new Error('Запись микрофона недоступна. Откройте переводчик в приложении SideWidget.');
   await invoke('translation.microphone');
   if(current.current!==session)return;
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

 async function translate(){
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
   const audio=await encode(blob);
   if(current.current!==session)return;
   session.request=true;
   const result=await invoke<{transcript:string;text:string}>('translation.voice',{audio,source,target});
   if(current.current===session){setValue(result);finish(session);}
  }catch(e){
   finish(session,e instanceof Error?e.message:'Не удалось выполнить перевод');
  }
 }

 async function speak(){
  if(!active||!value||current.current)return;
  const session:Session={request:true};
  current.current=session;
  setPhase('loadingAudio');
  setError('');
  try{
   const response=await invoke<{audio:string}>('translation.speak',{text:value.text,target});
   if(current.current!==session)return;
   session.request=false;
   const bytes=Uint8Array.from(atob(response.audio),c=>c.charCodeAt(0));
   const url=URL.createObjectURL(new Blob([bytes],{type:'audio/mpeg'}));
   const audio=new Audio();
   session.release=()=>{
    audio.pause();
    audio.removeAttribute('src');
    URL.revokeObjectURL(url);
   };
   audio.onended=()=>finish(session);
   audio.onerror=()=>finish(session,'Не удалось воспроизвести перевод.');
   audio.src=url;
   await audio.play();
   if(current.current===session)setPhase('playing');
  }catch(error){
   finish(session,session.request&&error instanceof Error?error.message:'Не удалось воспроизвести перевод.');
  }
 }

 return {phase,error,value,start,translate,speak,cancel,clear:()=>{setValue(undefined);setError('');}};
}
