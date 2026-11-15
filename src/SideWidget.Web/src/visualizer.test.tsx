// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {VisualizerSettingsPanel,defaultVisualizer,sanitizeSpectrum,SpectrumGraphic,useAudioSpectrum} from './AudioVisualization';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
afterEach(()=>{cleanup();vi.useRealTimers();vi.resetAllMocks();});
it('bounds native spectrum data and renders silence without decorative waves',()=>{
 const values=sanitizeSpectrum([NaN,-1,Infinity,.5,2]);
 expect(values).toHaveLength(32);expect(values.slice(0,5)).toEqual([0,0,0,.5,1]);
 const {container,rerender}=render(<SpectrumGraphic bands={Array(32).fill(0)} settings={defaultVisualizer}/>);
 expect(container.querySelector('path')?.getAttribute('d')).toBeNull();
 rerender(<SpectrumGraphic bands={Array(32).fill(.5)} settings={defaultVisualizer}/>);
 expect(container.querySelector('path')).toBeTruthy();
 rerender(<SpectrumGraphic bands={Array(32).fill(.5)} settings={{...defaultVisualizer,enabled:false}}/>);
 expect(container.querySelector('svg')).toBeNull();
});
it('saves brightness immediately with the complete options and reports errors',async()=>{
 vi.mocked(invoke).mockImplementation(async(action,params)=>action==='visualizer.get'?{available:true,bands:[],rms:0,sequence:1,settings:defaultVisualizer}: {settings:{visualizer:params}} as never);
 const saved=vi.fn();await act(async()=>render(<VisualizerSettingsPanel value={defaultVisualizer} onSaved={saved}/>));
 const brightness=screen.getByRole('slider',{name:/Яркость/});
 fireEvent.change(brightness,{target:{value:'63'}});
 expect(invoke).not.toHaveBeenCalledWith('visualizer.settings',expect.anything());
 await act(async()=>fireEvent.pointerUp(brightness));
 expect(invoke).toHaveBeenCalledWith('visualizer.settings',{...defaultVisualizer,brightness:.63});
 expect(saved).toHaveBeenCalled();
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Настройки не сохранены'));
 await act(async()=>fireEvent.click(screen.getByRole('checkbox',{name:'Показывать в свёрнутом баре'})));
 expect(screen.getByRole('alert').textContent).toBe('Настройки не сохранены');
});

it('stops spectrum bridge polling when the panel collapses',async()=>{
 vi.useFakeTimers();
 vi.mocked(invoke).mockResolvedValue({available:true,bands:Array(32).fill(.2),rms:.1,sequence:1,settings:defaultVisualizer});
 function Probe({active}:{active:boolean}){const frame=useAudioSpectrum(active);return <span>{frame.sequence}</span>;}
 const view=render(<Probe active/>);await act(async()=>{});
 expect(invoke).toHaveBeenCalledTimes(1);
 await act(async()=>vi.advanceTimersByTime(41));
 expect(invoke).toHaveBeenCalledTimes(2);
 view.rerender(<Probe active={false}/>);
 await act(async()=>vi.advanceTimersByTime(500));
 expect(invoke).toHaveBeenCalledTimes(2);
});

it('interpolates measured targets on display frames and quickly settles to silence',async()=>{
 vi.useFakeTimers();
 const view=render(<SpectrumGraphic bands={Array(32).fill(1)} settings={defaultVisualizer}/>);
 await act(async()=>vi.advanceTimersByTime(64));
 const first=view.container.querySelector('path')!.getAttribute('d');expect(first).toBeTruthy();
 await act(async()=>vi.advanceTimersByTime(64));
 expect(view.container.querySelector('path')!.getAttribute('d')).not.toBe(first);
 view.rerender(<SpectrumGraphic bands={Array(32).fill(0)} settings={defaultVisualizer}/>);
 await act(async()=>vi.advanceTimersByTime(400));
 expect(view.container.querySelector('path')!.getAttribute('d')).toBe('');
});
