const sampleRate=16000;
const maxSamples=30*sampleRate;

function readAudio(blob:Blob){
 return new Promise<ArrayBuffer>((resolve,reject)=>{
  const reader=new FileReader();
  reader.onerror=()=>reject(reader.error);
  reader.onload=()=>resolve(reader.result as ArrayBuffer);
  reader.readAsArrayBuffer(blob);
 });
}

export async function toDictationWav(blob:Blob){
 const context=new AudioContext({sampleRate});
 try{
  const decoded=await context.decodeAudioData(await readAudio(blob));
  const count=Math.min(decoded.length,maxSamples);
  if(decoded.sampleRate!==sampleRate||!count)throw new Error();
  const channels=Array.from({length:decoded.numberOfChannels},(_,index)=>decoded.getChannelData(index));
  const data=new ArrayBuffer(44+count*2);
  const wav=new DataView(data);
  wav.setUint32(0,0x52494646); // RIFF
  wav.setUint32(4,data.byteLength-8,true);
  wav.setUint32(8,0x57415645); // WAVE
  wav.setUint32(12,0x666d7420); // fmt
  wav.setUint32(16,16,true);
  wav.setUint16(20,1,true); // PCM
  wav.setUint16(22,1,true); // Mono
  wav.setUint32(24,sampleRate,true);
  wav.setUint32(28,sampleRate*2,true);
  wav.setUint16(32,2,true);
  wav.setUint16(34,16,true);
  wav.setUint32(36,0x64617461); // data
  wav.setUint32(40,count*2,true);
  for(let index=0;index<count;index++){
   const mix=channels.reduce((sum,channel)=>sum+channel[index],0)/channels.length;
   const sample=Math.max(-1,Math.min(1,mix));
   wav.setInt16(44+index*2,sample<0?sample*32768:sample*32767,true);
  }
  return new Blob([data],{type:'audio/wav'});
 }finally{
  await context.close();
 }
}
