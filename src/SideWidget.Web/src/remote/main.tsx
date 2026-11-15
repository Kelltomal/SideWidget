import {createRoot} from 'react-dom/client';
import {PhoneRemoteApp} from './PhoneRemoteApp';
import './remote.css';
import {resolveLocale,setLocale,t} from '../i18n';
setLocale(resolveLocale(navigator.language));
document.title=t("Пульт SideWidget");
document.querySelector('meta[name="description"]')?.setAttribute('content',t("Пульт музыки на вашем ПК через домашний Wi-Fi"));
createRoot(document.getElementById('root')!).render(<PhoneRemoteApp/>);
if('serviceWorker' in navigator&&window.isSecureContext&&location.protocol==='https:'){
 void navigator.serviceWorker.register('/remote/sw.js',{scope:'/',updateViaCache:'none'}).catch(()=>{});
}
