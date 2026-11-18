// @vitest-environment jsdom
import {act,cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {afterEach,beforeEach,expect,it,vi} from 'vitest';
import QRCode from 'qrcode';
import {PhoneRemoteSettings} from './PhoneRemoteSettings';
import {invoke} from './ui';
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
vi.mock('qrcode',()=>({default:{toDataURL:vi.fn().mockResolvedValue('data:image/png;base64,dGVzdA==')}}));
const info={enabled:false,running:false,port:47831,setupPort:47830,adapters:[{id:'home',name:'Ethernet',address:'192.168.31.29'}],startup:{enabled:false},access:{devices:[],pending:[]}};
afterEach(()=>{cleanup();vi.resetAllMocks();});
beforeEach(()=>{vi.mocked(QRCode.toDataURL).mockImplementation(()=>Promise.resolve('data:image/png;base64,dGVzdA=='));});
async function show(){render(<PhoneRemoteSettings/>);await act(async()=>{const details=document.querySelector('details')!;details.open=true;fireEvent(details,new Event('toggle'));});await screen.findByRole('button',{name:'Настроить сеть и подключить телефон'});}
it('configures network and shows permanent QR without invitation',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>action==='remote.get'?info:{...info,enabled:true,running:true,adapterId:'home',setupUrl:'http://192.168.31.29:47830/'} as never);
 await show();expect(invoke).not.toHaveBeenCalledWith('remote.setup',expect.anything());
 await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить сеть и подключить телефон'})));
 expect(invoke).toHaveBeenCalledWith('remote.setup',{adapterId:'home',port:47831,setupPort:47830,startup:true});
 await waitFor(()=>expect(screen.getByAltText('QR подключения телефона к SideWidget')).toBeTruthy());
 expect(vi.mocked(invoke).mock.calls.some(([action])=>action==='remote.invite')).toBe(false);
 expect((screen.getByRole('textbox',{name:'Адрес домашнего пульта'}) as HTMLInputElement).value).toBe('http://192.168.31.29:47830/');
 expect(screen.getByText('Пульт доступен в домашней сети')).toBeTruthy();
});
it('does not show a ready QR after UAC cancellation',async()=>{
 vi.mocked(invoke).mockImplementation(async action=>{if(action==='remote.get')return info as never;throw new Error('Подтверждение Windows отменено.');});
 await show();await act(async()=>fireEvent.click(screen.getByRole('button',{name:'Настроить сеть и подключить телефон'})));
 expect(screen.getByRole('alert').textContent).toContain('Подтверждение Windows отменено');
 expect(vi.mocked(invoke).mock.calls.some(([action])=>action==='remote.invite')).toBe(false);
 expect(screen.queryByAltText('QR подключения телефона к SideWidget')).toBeNull();
});
it('shows permanent QR immediately when settings reopen for running remote',async()=>{
 vi.mocked(invoke).mockResolvedValue({...info,running:true,enabled:true,setupUrl:'http://192.168.31.29:47830/'} as never);
 render(<PhoneRemoteSettings/>);await act(async()=>{const details=document.querySelector('details')!;details.open=true;fireEvent(details,new Event('toggle'));});
 expect(await screen.findByAltText('QR подключения телефона к SideWidget')).toBeTruthy();
 expect(vi.mocked(invoke).mock.calls.some(([action])=>action==='remote.invite'||action==='remote.setup')).toBe(false);
 expect(screen.queryByText('Новый QR подключения')).toBeNull();
});
