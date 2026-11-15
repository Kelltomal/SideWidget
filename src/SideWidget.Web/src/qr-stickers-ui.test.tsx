// @vitest-environment jsdom
import {afterEach,expect,it,vi} from 'vitest';
import {cleanup,fireEvent,render,screen,waitFor} from '@testing-library/react';
import {QrStickerPrint} from './QrStickerPrint';
import {invoke} from './ui';
const availability=vi.hoisted(()=>({native:true}));
vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),get nativeAvailable(){return availability.native;}}));
afterEach(()=>{cleanup();vi.resetAllMocks();availability.native=true;});
function setup(){render(<QrStickerPrint text={'  Привет\nмир  '} level="M"/>);fireEvent.click(screen.getByText('Печать на стикерах'));}
it('opens the native print document with physical page size and reports cancellation honestly',async()=>{
 vi.mocked(invoke).mockResolvedValue({opened:false});setup();
 fireEvent.change(screen.getByLabelText('Стикеров'),{target:{value:'2'}});
 fireEvent.click(screen.getByRole('button',{name:'Печатать стикеры'}));
 await waitFor(()=>expect(screen.getByText('Окно печати закрыто.')).toBeTruthy());
 expect(invoke).toHaveBeenCalledWith('qr.print',{html:expect.stringContaining('size:60mm 60mm')});
 const html=vi.mocked(invoke).mock.calls[0][1] as {html:string};
 expect(html.html.match(/class="sticker"/g)).toHaveLength(2);
});
it('disables print on empty numeric fields and restores it when valid',()=>{
 setup();fireEvent.change(screen.getByLabelText('Формат стикера'),{target:{value:'custom'}});
 fireEvent.change(screen.getByLabelText('Ширина, мм'),{target:{value:''}});
 expect((screen.getByRole('button',{name:'Печатать стикеры'}) as HTMLButtonElement).disabled).toBe(true);
 fireEvent.change(screen.getByLabelText('Ширина, мм'),{target:{value:'58'}});
 expect((screen.getByRole('button',{name:'Печатать стикеры'}) as HTMLButtonElement).disabled).toBe(false);
});
it('shows native failure and lets the user retry',async()=>{
 vi.mocked(invoke).mockRejectedValue(new Error('Печать недоступна'));setup();
 fireEvent.click(screen.getByRole('button',{name:'Печатать стикеры'}));
 await waitFor(()=>expect(screen.getByRole('alert').textContent).toContain('Печать недоступна'));
 expect((screen.getByRole('button',{name:'Печатать стикеры'}) as HTMLButtonElement).disabled).toBe(false);
});
it('reports blocked browser popups without claiming printing succeeded',async()=>{
 availability.native=false;const open=vi.spyOn(window,'open').mockReturnValue(null);setup();
 fireEvent.click(screen.getByRole('button',{name:'Печатать стикеры'}));
 await waitFor(()=>expect(screen.getByRole('alert').textContent).toContain('Разрешите всплывающие окна'));
 expect(invoke).not.toHaveBeenCalled();open.mockRestore();
});
