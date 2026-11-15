// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {RemotePlayer} from './PhoneRemoteApp';
import type {RemoteState} from './client';
const state:RemoteState={media:{available:true,sessions:[{id:'player',title:'Название композиции',artist:'Исполнитель',source:'Yandex Music',playing:true,positionSeconds:30,durationSeconds:180,canPlay:true,canPause:true,canNext:true,canPrevious:true,canSeek:true}]},audio:{available:true,volume:.5,muted:false}};
afterEach(cleanup);
it('disables every music and volume control when disconnected',()=>{
 render(<RemotePlayer state={state} connected={false} busy={false} selected="player" onSelect={vi.fn()} send={vi.fn()}/>);
 for(const control of [...screen.getAllByRole('button'),...screen.getAllByRole('slider')])expect((control as HTMLButtonElement).disabled).toBe(true);
 expect(screen.getByText('Последнее полученное состояние')).toBeTruthy();
});
it('sends next to the explicit selected session and respects capabilities',()=>{
 const send=vi.fn();render(<RemotePlayer state={{...state,media:{...state.media,sessions:[{...state.media.sessions[0],canPrevious:false}]}}} connected busy={false} selected="player" onSelect={vi.fn()} send={send}/>);
 expect((screen.getByRole('button',{name:'Предыдущая композиция'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.click(screen.getByRole('button',{name:'Следующая композиция'}));expect(send).toHaveBeenCalledWith({command:'next',id:'player'});
});
it('requires explicit selection when the previous session has closed',()=>{
 const send=vi.fn();render(<RemotePlayer state={state} connected busy={false} selected="closed-player" onSelect={vi.fn()} send={send}/>);
 expect(screen.getByRole('combobox',{name:'Проигрыватель'})).toBeTruthy();
 expect((screen.getByRole('button',{name:'Воспроизвести'}) as HTMLButtonElement).disabled).toBe(true);
});
it('commits volume once and cancels an interrupted drag',async()=>{
 const send=vi.fn();render(<RemotePlayer state={state} connected busy={false} selected="player" onSelect={vi.fn()} send={send}/>);
 const slider=screen.getByRole('slider',{name:'Общая громкость ПК'});
 fireEvent.pointerDown(slider);fireEvent.change(slider,{target:{value:'0.7'}});expect(send).not.toHaveBeenCalled();
 await act(async()=>fireEvent.pointerUp(slider));expect(send).toHaveBeenCalledTimes(1);expect(send).toHaveBeenLastCalledWith({command:'volume',volume:.7});
 fireEvent.pointerDown(slider);fireEvent.change(slider,{target:{value:'0.9'}});fireEvent.pointerCancel(slider);fireEvent.blur(slider);expect(send).toHaveBeenCalledTimes(1);
});
