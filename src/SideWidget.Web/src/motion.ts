/** Keep frequent keyboard actions immediate; enable tactile motion for pointer input. */
export function installMotionInput(root:HTMLElement) {
 root.dataset.motionInput='keyboard';
 const pointer=()=>{root.dataset.motionInput='pointer';};
 const keyboard=()=>{root.dataset.motionInput='keyboard';};
 document.addEventListener('pointerdown',pointer,true);
 document.addEventListener('keydown',keyboard,true);
 return ()=>{
  document.removeEventListener('pointerdown',pointer,true);
  document.removeEventListener('keydown',keyboard,true);
  delete root.dataset.motionInput;
 };
}
