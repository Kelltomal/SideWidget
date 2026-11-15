// @vitest-environment jsdom
import {act,cleanup,render,screen} from '@testing-library/react';
import {afterEach,expect,it,vi} from 'vitest';
import {Metrics} from './WindowsPanels';
import {invoke} from './ui';

vi.mock('./ui',async original=>({...await original<typeof import('./ui')>(),invoke:vi.fn(),nativeAvailable:true}));
afterEach(()=>{cleanup();vi.resetAllMocks();});
async function show(gpuPercent:number|null,gpuMemoryUsed:number|null,compact=false,gpuMemoryTotal:number|null=8589934592){
 vi.mocked(invoke).mockResolvedValue({cpuPercent:10,memoryUsed:1073741824,memoryTotal:2147483648,uptimeSeconds:100,network:null,disks:[],battery:{available:false},gpuAvailable:gpuPercent!=null||gpuMemoryUsed!=null,gpuPercent,gpuMemoryUsed,gpuMemoryTotal});
 await act(async()=>render(<Metrics expanded compact={compact}/>));
}
it.each([true,false])('shows GPU load and dedicated memory, compact=%s',async compact=>{
 await show(37.2,1610612736,compact);
 expect(screen.getByText('GPU').nextElementSibling?.textContent).toBe('37%');
 expect(screen.getByText('Видеопамять').nextElementSibling?.textContent).toBe('1.5 ГБ / 8.0 ГБ');
});
it('distinguishes idle GPU and empty memory from unavailable counters',async()=>{
 await show(0,0);
 expect(screen.getByText('GPU').nextElementSibling?.textContent).toBe('0%');
 expect(screen.getByText('Видеопамять').nextElementSibling?.textContent).toBe('0.0 МБ / 8.0 ГБ');
});
it('handles unavailable metrics independently',async()=>{
 await show(null,1073741824);
 expect(screen.getByText('GPU').nextElementSibling?.textContent).toBe('Недоступно');
 expect(screen.getByText('Видеопамять').nextElementSibling?.textContent).toBe('1.0 ГБ / 8.0 ГБ');
});

it('keeps used memory when total capacity is unavailable',async()=>{
 await show(10,1073741824,false,null);
 expect(screen.getByText('Видеопамять').nextElementSibling?.textContent).toBe('1.0 ГБ / Недоступно');
});
it('keeps total capacity when usage is unavailable',async()=>{
 await show(null,null);
 expect(screen.getByText('Видеопамять').nextElementSibling?.textContent).toBe('Недоступно / 8.0 ГБ');
});

it('uses the current PC metrics heading',async()=>{
 await show(10,1073741824);
 expect(screen.getByRole('heading',{name:'Текущие показатели ПК'})).toBeTruthy();
});
