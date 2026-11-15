// @vitest-environment jsdom
import {describe,expect,it} from 'vitest';
import {installMotionInput} from './motion';

describe('motion input',()=>{
 it('switches pointer feedback off before keyboard navigation and removes listeners on cleanup',()=>{
  const root=document.createElement('div');document.body.append(root);
  const dispose=installMotionInput(root);
  expect(root.dataset.motionInput).toBe('keyboard');
  document.dispatchEvent(new Event('pointerdown'));
  expect(root.dataset.motionInput).toBe('pointer');
  document.dispatchEvent(new KeyboardEvent('keydown',{key:'Tab'}));
  expect(root.dataset.motionInput).toBe('keyboard');
  dispose();document.dispatchEvent(new Event('pointerdown'));
  expect(root.hasAttribute('data-motion-input')).toBe(false);root.remove();
 });
});
