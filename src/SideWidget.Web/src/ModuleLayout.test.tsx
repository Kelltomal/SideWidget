// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {useState} from 'react';
import {ModuleLayout} from './ModuleLayout';
import type {Snapshot} from './types';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),nativeAvailable:true,invoke:vi.fn()}));
afterEach(()=>{cleanup();vi.resetAllMocks();});
const initial={settings:{width:444,height:620,hideDelay:650,openDelay:100,hotkey:'Ctrl+Alt+Space',monitor:0,position:1,largeMode:false,order:['search','hidden','music'],disabled:['hidden']},calculatorHistory:[],colorHistory:[],unitFavorites:[],apps:[],drafts:{diary:''},selly:{configured:false,message:''}} satisfies Snapshot;
function Fixture({editing=true}:{editing?:boolean}){const [state,setState]=useState<Snapshot>(initial);return <ModuleLayout state={state} setState={setState} editing={editing} finish={vi.fn()} modules={['search','hidden','music'].map(id=>[id,id])} renderModule={id=><span>{id}</span>}/>;}
it('reorders by drag and persists immediately while retaining hidden modules',async()=>{
 vi.mocked(invoke).mockResolvedValue(initial);const view=render(<Fixture/>);
 const source=view.container.querySelector('[data-module="music"]')!;const target=view.container.querySelector('[data-module="search"]')!;
 fireEvent.dragStart(source,{dataTransfer:{setData:vi.fn(),effectAllowed:''}});
 await act(async()=>{fireEvent.dragOver(target);fireEvent.drop(target);});
 expect(invoke).toHaveBeenCalledWith('settings.save',expect.objectContaining({order:['music','search','hidden']}));
 expect(Array.from(view.container.querySelectorAll('[data-module]')).map(node=>node.getAttribute('data-module'))).toEqual(['music','search']);
});
it('prevents dragging outside edit mode',()=>{const view=render(<Fixture editing={false}/>);expect(view.container.querySelector('[data-module]')?.getAttribute('draggable')).toBe('false');expect(screen.queryByRole('button',{name:/Переместить/})).toBeNull();});
it('supports keyboard reorder and retries failed persistence on window blur',async()=>{
 vi.mocked(invoke).mockRejectedValueOnce(new Error('Нет связи')).mockResolvedValue(initial);render(<Fixture/>);
 await act(async()=>fireEvent.keyDown(screen.getByRole('button',{name:'Переместить: music'}),{key:'ArrowUp',altKey:true}));
 expect(screen.getByRole('alert').textContent).toContain('Нет связи');
 await act(async()=>fireEvent.blur(window));expect(invoke).toHaveBeenCalledTimes(2);
});
