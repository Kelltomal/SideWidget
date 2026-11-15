"""Measure a Windows build with an isolated SideWidget profile and real hotkeys."""

import argparse
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import statistics
import subprocess
import time


user32 = ctypes.windll.user32
user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
user32.GetForegroundWindow.restype = wintypes.HWND
user32.keybd_event.argtypes = [wintypes.BYTE, wintypes.BYTE, wintypes.DWORD, ctypes.c_size_t]
WM_CLOSE = 0x0010


def wait_for(check, description, timeout=12):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if result := check():
            return result
        time.sleep(0.02)
    raise RuntimeError(f"Timed out: {description}")


def windows(pid):
    handles = []

    @ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    def collect(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid:
            title = ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(hwnd, title, len(title))
            if title.value.startswith('SideWidget'):
                handles.append(hwnd)
        return True

    user32.EnumWindows(collect, 0)
    return handles


def hotkey():
    keys = (0x11, 0x12, 0x20)  # Ctrl, Alt, Space
    for key in keys:
        user32.keybd_event(key, 0, 0, 0)
    for key in reversed(keys):
        user32.keybd_event(key, 0, 2, 0)


def records(path, pid):
    if not path.exists():
        return []
    return [row for line in path.read_text(encoding='utf-8').splitlines() if line.endswith('}')
            and (row := json.loads(line))['pid'] == pid]


def measure(exe, profile, starts, openings, point):
    env = dict(os.environ, SIDEWIDGET_MEASUREMENT_DIRECTORY=str(profile))
    path = profile / 'opening.ndjson'
    results = []
    original_pointer = wintypes.POINT()
    user32.GetCursorPos(ctypes.byref(original_pointer))
    try:
        for _ in range(starts):
            process = subprocess.Popen([str(exe), '--measure-opening'], cwd=exe.parent, env=env)
            try:
                def stage(name, request=None):
                    if process.poll() is not None:
                        raise RuntimeError('SideWidget exited. Close the other SideWidget instance before measuring.')
                    return next((row for row in records(path, process.pid)
                                 if row['stage'] == name and row['id'] == request
                                 and (request is not None or row['primary'])), None)

                header = wait_for(lambda: stage('header.shown'), 'native header')
                ready = wait_for(lambda: stage('home.ready'), 'primary WebView2 home')
                user32.SetCursorPos(*point)
                samples = []
                for request in range(1, openings + 1):
                    hotkey()
                    expanded = wait_for(lambda: stage('hotkey.expanded', request), 'hotkey expansion')
                    focused = wait_for(lambda: stage('hotkey.searchFocused', request), 'search focus')
                    if not focused['foreground']:
                        raise RuntimeError('Search focus acknowledged without a foreground window.')
                    samples.append({'expandedMs': expanded['elapsedMs'], 'searchFocusedMs': focused['elapsedMs'],
                                    'primary': focused['primary']})
                    hotkey()
                    time.sleep(0.22)
                results.append({'pid': process.pid, 'headerMs': header['elapsedMs'], 'readyMs': ready['elapsedMs'],
                                'windows': sum(row['stage'] == 'home.ready' for row in records(path, process.pid)),
                                'openings': samples})
            finally:
                handles = windows(process.pid)
                if handles:
                    user32.PostMessageW(handles[0], WM_CLOSE, 0, 0)
                try:
                    process.wait(timeout=4)
                except subprocess.TimeoutExpired:
                    process.terminate()
                    process.wait(timeout=4)
    finally:
        user32.SetCursorPos(original_pointer.x, original_pointer.y)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('executable', type=Path)
    parser.add_argument('profile', type=Path)
    parser.add_argument('--starts', type=int, default=3)
    parser.add_argument('--openings', type=int, default=5)
    parser.add_argument('--pointer', nargs=2, type=int, default=(100, 100))
    args = parser.parse_args()
    if args.starts < 1 or args.openings < 1:
        parser.error('At least one start and opening are required.')
    profile = args.profile.resolve()
    if profile == Path(os.environ['LOCALAPPDATA'], 'SideWidget').resolve():
        parser.error('Use a dedicated measurement profile.')
    profile.mkdir(parents=True, exist_ok=True)
    results = measure(args.executable.resolve(), profile, args.starts, args.openings, args.pointer)
    openings = [opening for result in results for opening in result['openings']]
    summary = {field: round(statistics.median(row[field] for row in rows), 1)
               for rows, fields in [(results, ('headerMs', 'readyMs')), (openings, ('expandedMs', 'searchFocusedMs'))]
               for field in fields}
    print(json.dumps({'runs': results, 'medianMs': summary}, indent=2))


if __name__ == '__main__':
    main()
