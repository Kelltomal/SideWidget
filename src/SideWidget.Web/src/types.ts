export type VisualizerOptions={enabled:boolean;showInHeader:boolean;brightness:number;sensitivity:number;style:'wave'|'bars'};
export type AudioSpectrumFrame={available:boolean;message?:string;bands:number[];rms:number;sequence:number;settings:VisualizerOptions};
export type Settings = {
  displayIds?:string[]|null;
  displayPositions?:Record<string,number>|null;
  pinnedTools?:string[]|null;
  visualizer?:VisualizerOptions;
  theme?:'dark'|'light'|'system';
  width:number; height:number; hideDelay:number; openDelay:number; hotkey:string;
  monitor:number; position:number; largeMode:boolean; order:string[]; disabled:string[];
};
export type Calculation = {id:string; expression:string; result:string; time:string};
export type AppShortcut = {id:string; name:string; path:string; icon?:string};
export type Snapshot = {
 appVersion?: string;
  displays?:DisplayDescriptor[];
  settings:Settings; calculatorHistory:Calculation[]; colorHistory:string[];
  unitFavorites:{from:string;to:string}[]; apps:AppShortcut[]; drafts:{diary:string};
  launcherUndoAvailable?:boolean;
  selly:{configured:boolean;message:string};
};
export type DisplayDescriptor={id:string;name:string;primary:boolean};
export type Invoke = <T>(action:string, params?:object)=>Promise<T>;
export type MediaSession = {
  id:string; title:string; artist:string; source:string; playing:boolean;
  positionSeconds:number|null; durationSeconds:number|null; canPlay:boolean; canPause:boolean;
  canPrevious:boolean; canNext:boolean; canSeek:boolean; artwork?:string; artworkKey?:string;
};
export type MediaState = {available:boolean;sessions:MediaSession[];message?:string};
export type AudioState = {
  available:boolean;volume:number|null;muted:boolean|null;microphoneMuted?:boolean|null;
  devices:{id:string;name:string;flow:string;default:boolean}[];
  sessions:{id:string;name:string;volume:number;muted:boolean}[];message?:string;
};
export type SystemState = {
  cpuPercent:number|null;memoryUsed:number|null;memoryTotal:number|null;uptimeSeconds:number;
  network:{name:string;receiveBytesPerSecond:number|null;sendBytesPerSecond:number|null}|null;
  disks:{name:string;total:number;free:number}[];
  battery:{available:boolean;percent?:number|null;charging?:boolean|null;ac?:boolean|null};gpuAvailable:boolean;
  gpuPercent?:number|null;gpuMemoryUsed?:number|null;gpuMemoryTotal?:number|null;
};
