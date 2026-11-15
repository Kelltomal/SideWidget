import {describe,it,expect,vi} from 'vitest';
import {Bridge, BridgeError, type Host} from './bridge';
describe('native bridge',()=>{
  it('rejects a browser without inventing data', async()=>{
    const b=new Bridge(); await expect(b.request('bridge.ready').promise).rejects.toThrow('нативном');
  });
  it('correlates results and ignores unrelated or late responses',async()=>{
    let receive:(e:MessageEvent)=>void=()=>{};let sent:any;
    const host:Host={postMessage:m=>sent=m,addEventListener:(_,f)=>receive=f};const b=new Bridge(host);
    const request=b.request<number>('api.execute',{id:'health'});
    expect(sent.actionId).toBe('api.execute');expect(sent.params).toEqual({id:'health'});
    receive({data:{protocolVersion:1,requestId:'unrelated',status:'result',data:9}} as MessageEvent);
    receive({data:{protocolVersion:1,requestId:request.id,status:'result',data:7}} as MessageEvent);
    expect(await request.promise).toBe(7);
    receive({data:{protocolVersion:1,requestId:request.id,status:'result',data:99}} as MessageEvent);
  });
  it('bounds waiting on a missing terminal response',async()=>{
    vi.useFakeTimers();const b=new Bridge({postMessage:()=>{},addEventListener:()=>{}});
    const result=b.request('api.execute').promise;const assertion=expect(result).rejects.toThrow('ответ');
    await vi.advanceTimersByTimeAsync(180001);await assertion;vi.useRealTimers();
  });
  it('preserves structured cancellation separately from a genuine native failure',async()=>{
    let receive:(e:MessageEvent)=>void=()=>{};
    const bridge=new Bridge({postMessage:()=>{},addEventListener:(_,listener)=>{receive=listener;}});
    const task=bridge.request('color.pick');
    const assertion=expect(task.promise).rejects.toMatchObject({name:'BridgeError',code:'cancelled',message:'Выбор цвета отменён'});
    receive({data:{protocolVersion:1,requestId:task.id,status:'error',error:{code:'cancelled',message:'Выбор цвета отменён'}}} as MessageEvent);
    await assertion;expect(new BridgeError('permission','Нет доступа').code).toBe('permission');
  });
  it('keeps a website translation pending while a person solves its challenge',async()=>{
    vi.useFakeTimers();
    try {
      let receive:(e:MessageEvent)=>void=()=>{};
      const bridge=new Bridge({postMessage:()=>{},addEventListener:(_,listener)=>{receive=listener;}});
      const task=bridge.request('translation.run',{provider:'yandex'});
      let settled=false;void task.promise.then(()=>{settled=true;},()=>{settled=true;});
      await vi.advanceTimersByTimeAsync(5*60000);
      expect(settled).toBe(false);
      receive({data:{protocolVersion:1,requestId:task.id,status:'result',data:{text:'Привет'}}} as MessageEvent);
      expect(await task.promise).toEqual({text:'Привет'});
    } finally {vi.useRealTimers();}
  });
  it('still bounds a lost website translation response',async()=>{
    vi.useFakeTimers();
    try {
      const bridge=new Bridge({postMessage:()=>{},addEventListener:()=>{}});
      const assertion=expect(bridge.request('translation.run',{provider:'google'}).promise).rejects.toThrow('ответ');
      await vi.advanceTimersByTimeAsync(12*60000+1);await assertion;
    } finally {vi.useRealTimers();}
  });
});
