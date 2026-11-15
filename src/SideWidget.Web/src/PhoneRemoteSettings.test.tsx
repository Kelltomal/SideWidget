// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {PhoneRemoteSettings} from './PhoneRemoteSettings';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
vi.mock('qrcode',()=>({default:{toDataURL:vi.fn().mockResolvedValue('data:image/png;base64,dGVzdA==')}}));
const info={enabled:false,running:false,port:47831,setupPort:47830,adapters:[{id:'home',name:'Ethernet',address:'192.168.31.29'}],startup:{enabled:false},access:{devices:[],pending:[]}};
afterEach(()=>{cleanup();vi.resetAllMocks();});
async function show(){render(<PhoneRemoteSettings/>);await act(async()=>{const details=document.querySelector('details')!;details.open=true;fireEvent(details,new Event('toggle'));});await screen.findByRole('button',{name:'Настроить сеть и подключить телефон'});}
it('configures network and enables remote with one action before generating QR',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>action==='remote.get'?info:action==='remote.setup'?{...info,enabled:true,running:true,adapterId:'home'}:{setupUrl:'http://192.168.31.29:47830/#invite=test',secureUrl:'https://192.168.31.29:47831/#invite=test',expires:new Date(Date.now()+300000).toISOString()} as never);
 await show();expect(invoke).not.toHaveBeenCalledWith('remote.setup',expect.anything());
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить сеть и подключить телефон'})));
 expect(invoke).toHaveBeenCalledWith('remote.setup',{adapterId:'home',port:47831,setupPort:47830,startup:true});
 await waitFor(()=>expect(invoke).toHaveBeenCalledWith('remote.invite'));
 expect(screen.getByText('Пульт доступен в домашней сети')).toBeTruthy();
});
it('does not show a ready QR after UAC cancellation',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>{if(action==='remote.get')return info as never;throw new Error('Подтверждение Windows отменено.');});
 await show();await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить сеть и подключить телефон'})));
 expect(screen.getByRole('alert').textContent).toContain('Подтверждение Windows отменено');
 expect(vi.mocked(invoke).mock.calls.some(([action])=>action==='remote.invite')).toBe(false);
 expect(screen.queryByAltText('QR подключения телефона к SideWidget')).toBeNull();
});
