import {t,locale} from './i18n';
import {useEffect,useState} from 'react';
import QRCode from 'qrcode';
import {invoke,nativeAvailable,Notice,useAction} from './ui';
import './phone-remote-settings.css';

type RemoteInfo={enabled:boolean;running:boolean;adapterId?:string;port:number;setupPort:number;adapters:{id:string;name:string;address:string}[];secureUrl?:string;setupUrl?:string;fingerprint?:string;message?:string;startup:{enabled:boolean;message?:string};access:{devices:{id:string;name:string;lastSeen:string}[];pending:{id:string;name:string;code:string}[]}};
type Invitation={setupUrl:string;secureUrl:string;expires:string};

export function PhoneRemoteSettings({active=true}:{active?:boolean}){
 const [open,setOpen]=useState(false);const [info,setInfo]=useState<RemoteInfo>();const [adapter,setAdapter]=useState('');const [port,setPort]=useState(47831);const [setupPort,setSetupPort]=useState(47830);const [startup,setStartup]=useState(true);
 const [invite,setInvite]=useState<Invitation>();const [qr,setQr]=useState('');const [qrSecure,setQrSecure]=useState(false);const [dirty,setDirty]=useState(false);const [now,setNow]=useState(Date.now());
 const {run,busy,error}=useAction();
 function accept(next:RemoteInfo,reset=false){setInfo(next);if(reset){setAdapter(next.adapterId||next.adapters[0]?.id||'');setPort(next.port);setSetupPort(next.setupPort);setStartup(next.enabled?next.startup.enabled:true);setDirty(false);}}
 useEffect(()=>{
  if(!open||!active||!nativeAvailable)return;let alive=true;let timer:ReturnType<typeof setTimeout>;let initial=true;
  async function poll(){try{const next=await invoke<RemoteInfo>('remote.get');if(alive){setInfo(next);if(initial){accept(next,true);initial=false;}setNow(Date.now());}}catch{}finally{if(alive)timer=setTimeout(poll,2000);}}
  void run(async()=>{const next=await invoke<RemoteInfo>('remote.get');if(alive){accept(next,true);initial=false;}}).then(()=>{if(alive)timer=setTimeout(poll,2000);});
  return()=>{alive=false;clearTimeout(timer);};
 },[open,active]);
 useEffect(()=>{let alive=true;setQr('');if(invite)void QRCode.toDataURL(qrSecure?invite.secureUrl:invite.setupUrl,{width:256,margin:3,color:{dark:'#101416',light:'#ffffff'},errorCorrectionLevel:'M'}).then(value=>{if(alive)setQr(value);});return()=>{alive=false;};},[invite,qrSecure]);
 async function configure(enabled:boolean){await run(async()=>{const next=await invoke<RemoteInfo>(enabled?'remote.setup':'remote.configure',{...enabled?{}:{enabled:false},adapterId:adapter,port,setupPort,startup});accept(next,true);setInvite(undefined);if(next.running)setInvite(await invoke<Invitation>('remote.invite'));});}
 async function createInvite(){await run(async()=>{setInvite(await invoke<Invitation>('remote.invite'));setNow(Date.now());});}
 async function decide(id:string,approved:boolean){await run(async()=>accept(await invoke<RemoteInfo>('remote.decide',{id,approved})));}
 async function revoke(id:string){await run(async()=>accept(await invoke<RemoteInfo>('remote.revoke',{id})));}
 const expired=invite&&new Date(invite.expires).getTime()<=now;
 return <details className="phone-remote-settings" onToggle={event=>setOpen(event.currentTarget.open)}><summary>{t("Пульт на телефоне")}</summary>{open&&<div className="phone-remote-content">
  {!nativeAvailable?<p>{t("Настройка доступна в SideWidget для Windows.")}</p>:<>
   <p>{t("Управляйте музыкой с телефона в домашней сети. Подключите телефон один раз — затем пульт запомнит доступ.")}</p>
   {info&&<><p role="status">{info.running?t("Пульт доступен в домашней сети"):info.enabled?t("Пульт включён, ожидает сеть"):t("Пульт выключен")}</p>
    <label>{t("Домашний сетевой адаптер")}<select value={adapter} disabled={busy} onChange={e=>{setAdapter(e.target.value);setDirty(true);}}><option value="" disabled>{t("Выберите Wi-Fi или Ethernet")}</option>{info.adapters.map(a=><option key={a.id+':'+a.address} value={a.id}>{a.name} · {a.address}</option>)}</select></label>
    {!info.adapters.length&&<Notice>{t("Подключите ПК к домашнему роутеру через Wi-Fi или Ethernet. В VPN разрешите доступ к LAN.")}</Notice>}
    {!info.running&&<small>{t("Кнопка ниже настроит частный профиль домашней сети и правило брандмауэра только для этого приложения, двух портов и домашней подсети. Подтвердите стандартный запрос прав администратора Windows.")}</small>}
    <label className="checkbox"><input type="checkbox" checked={startup} disabled={busy} onChange={e=>{setStartup(e.target.checked);setDirty(true);}}/>{t("Запускать SideWidget при входе в Windows")}</label>
    {info.startup.message&&<Notice>{t(info.startup.message)}</Notice>}
    <details><summary>{t("Сетевые порты")}</summary><div className="form-row"><label>HTTPS<input type="number" min={1024} max={65535} value={port} disabled={busy} onChange={e=>{setPort(Number(e.target.value));setDirty(true);}}/></label><label>{t("Первичная настройка")}<input type="number" min={1024} max={65535} value={setupPort} disabled={busy} onChange={e=>{setSetupPort(Number(e.target.value));setDirty(true);}}/></label></div></details>
    <div className="actions"><button type="button" className="primary" disabled={busy||(!adapter&&!info.enabled)} onClick={()=>void configure(info.running&&!dirty?false:true)}>{busy?t("Настраиваем…"):info.running?(dirty?t("Применить настройки"):t("Выключить пульт")):t("Настроить сеть и подключить телефон")}</button>{info.running&&<button type="button" disabled={busy} onClick={()=>void createInvite()}>{t("Новый QR подключения")}</button>}</div>
    {info.message&&<Notice error>{info.message}</Notice>}
    {info.running&&<><label>{t("Адрес установленного пульта")}<input readOnly value={info.secureUrl||''} onFocus={e=>e.currentTarget.select()}/></label><small>{t("Закрепите IP компьютера в настройках DHCP роутера, чтобы ссылка не менялась. После запуска ПК откройте пульт на телефоне — связь восстановится сама.")}</small>
     {invite&&<div className="remote-qr"><div role="group" aria-label={t("Тип подключения")} className="actions"><button type="button" aria-pressed={!qrSecure} onClick={()=>setQrSecure(false)}>{t("Первый раз")}</button><button type="button" aria-pressed={qrSecure} onClick={()=>setQrSecure(true)}>{t("Сертификат установлен")}</button></div>{expired?<Notice>{t("QR истёк. Нажмите «Новый QR подключения».")}</Notice>:<>{qr&&<img src={qr} width={256} height={256} alt={t("QR подключения телефона к SideWidget")}/>}<small>{t("Сканируйте камерой телефона. Запрос действует пять минут.")}</small><input aria-label={t("Ссылка подключения телефона")} readOnly value={qrSecure?invite.secureUrl:invite.setupUrl} onFocus={e=>e.currentTarget.select()}/></>}</div>}
     <details><summary>{t("Установка приложения на Android")}</summary><p>{t("Первый QR откроет инструкцию и скачивание вашего сертификата. Сверьте SHA-256 ниже, установите сертификат центра сертификации в Android, затем откройте HTTPS-пульт. В Chrome выберите «Установить приложение». Не отключайте проверку HTTPS.")}</p><small>{t("Сертификат добавляет доверенный центр сертификации на телефон. Устанавливайте только свой после сверки отпечатка.")}</small><code className="remote-fingerprint">{info.fingerprint?.match(/.{1,4}/g)?.join(' ')}</code></details>
     <small>{t("VPN не меняет выбранный адаптер. Если его kill switch блокирует домашнюю сеть, разрешите LAN в VPN. В Windows Firewall разрешите SideWidget только для частной сети.")}</small>
    </>}
    {info.access.pending.map(p=><div className="remote-device" key={p.id}><strong>{p.name}</strong><p>{t("Сверьте код на телефоне:")} <b className="remote-pair-code">{p.code}</b></p><div className="actions"><button type="button" className="primary" disabled={busy} onClick={()=>void decide(p.id,true)}>{t("Разрешить")}</button><button type="button" disabled={busy} onClick={()=>void decide(p.id,false)}>{t("Отклонить")}</button></div></div>)}
    {info.access.devices.length>0&&<h3>{t("Допущенные телефоны")}</h3>}{info.access.devices.map(d=><div className="remote-device" key={d.id}><div><strong>{d.name}</strong><small>{t("Последняя связь: {time}",{time:new Date(d.lastSeen).toLocaleString(locale())})}</small></div><button type="button" disabled={busy} onClick={()=>void revoke(d.id)}>{t("Отозвать доступ")}</button></div>)}
   </>}{!info&&!error&&<Notice>{t("Получаем настройки…")}</Notice>}
  </>}{error&&<Notice error>{error}</Notice>}
 </div>}</details>;
}
