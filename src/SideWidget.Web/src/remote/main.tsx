import {createRoot} from 'react-dom/client';
import {PhoneRemoteApp} from './PhoneRemoteApp';
import './remote.css';
createRoot(document.getElementById('root')!).render(<PhoneRemoteApp/>);
if('serviceWorker' in navigator&&window.isSecureContext&&location.protocol==='https:'){
 void navigator.serviceWorker.register('/remote/sw.js',{scope:'/',updateViaCache:'none'}).catch(()=>{});
}
