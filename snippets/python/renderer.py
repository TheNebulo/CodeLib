import sys
import ctypes as ct
import ctypes.wintypes as w 

def errcheck(result, func, args):
    if result is None or result == 0:
        raise ct.WinError(ct.get_last_error())
    return result

def minusonecheck(result, func, args):
    if result == -1:
        raise ct.WinError(ct.get_last_error())
    return result

LRESULT = ct.c_int64
HCURSOR = ct.c_void_p

WNDPROC = ct.WINFUNCTYPE(LRESULT, w.HWND, w.UINT, w.WPARAM, w.LPARAM)

def MAKEINTRESOURCE(x):
    return w.LPCWSTR(x)

class WNDCLASS(ct.Structure):
    _fields_ = (('style', w.UINT),
                ('lpfnWndProc', WNDPROC),
                ('cbClsExtra', ct.c_int),
                ('cbWndExtra', ct.c_int),
                ('hInstance', w.HINSTANCE),
                ('hIcon', w.HICON),
                ('hCursor', HCURSOR),
                ('hbrBackground', w.HBRUSH),
                ('lpszMenuName', w.LPCWSTR),
                ('lpszClassName', w.LPCWSTR))

class PAINTSTRUCT(ct.Structure):
    _fields_ = (('hdc', w.HDC),
                ('fErase', w.BOOL),
                ('rcPaint', w.RECT),
                ('fRestore', w.BOOL),
                ('fIncUpdate', w.BOOL),
                ('rgbReserved', w.BYTE * 32))

kernel32 = ct.WinDLL('kernel32', use_last_error=True)
GetModuleHandle = kernel32.GetModuleHandleW
GetModuleHandle.argtypes = w.LPCWSTR,
GetModuleHandle.restype = w.HMODULE
GetModuleHandle.errcheck = errcheck

user32 = ct.WinDLL('user32', use_last_error=True)
CreateWindowEx = user32.CreateWindowExW
CreateWindowEx.argtypes = w.DWORD, w.LPCWSTR, w.LPCWSTR, w.DWORD, ct.c_int, ct.c_int, ct.c_int, ct.c_int, w.HWND, w.HMENU, w.HINSTANCE, w.LPVOID
CreateWindowEx.restype = w.HWND
CreateWindowEx.errcheck = errcheck
LoadIcon = user32.LoadIconW
LoadIcon.argtypes = w.HINSTANCE, w.LPCWSTR
LoadIcon.restype = w.HICON
LoadIcon.errcheck = errcheck
LoadCursor = user32.LoadCursorW
LoadCursor.argtypes = w.HINSTANCE, w.LPCWSTR
LoadCursor.restype = HCURSOR
LoadCursor.errcheck = errcheck
RegisterClass = user32.RegisterClassW
RegisterClass.argtypes = ct.POINTER(WNDCLASS),
RegisterClass.restype = w.ATOM
RegisterClass.errcheck = errcheck
ShowWindow = user32.ShowWindow
ShowWindow.argtypes = w.HWND, ct.c_int
ShowWindow.restype = w.BOOL
UpdateWindow = user32.UpdateWindow
UpdateWindow.argtypes = w.HWND,
UpdateWindow.restype = w.BOOL
UpdateWindow.errcheck = errcheck
GetMessage = user32.GetMessageW
GetMessage.argtypes = ct.POINTER(w.MSG), w.HWND, w.UINT, w.UINT
GetMessage.restype = w.BOOL
GetMessage.errcheck = minusonecheck
TranslateMessage = user32.TranslateMessage
TranslateMessage.argtypes = ct.POINTER(w.MSG),
TranslateMessage.restype = w.BOOL
DispatchMessage = user32.DispatchMessageW
DispatchMessage.argtypes = ct.POINTER(w.MSG),
DispatchMessage.restype = LRESULT
BeginPaint = user32.BeginPaint
BeginPaint.argtypes = w.HWND, ct.POINTER(PAINTSTRUCT)
BeginPaint.restype = w.HDC
GetClientRect = user32.GetClientRect
GetClientRect.argtypes = w.HWND, ct.POINTER(w.RECT)
GetClientRect.restype = w.BOOL
GetClientRect.errcheck = errcheck
DrawText = user32.DrawTextW
DrawText.argtypes = w.HDC, w.LPCWSTR, ct.c_int, ct.POINTER(w.RECT), w.UINT
DrawText.restype = ct.c_int
EndPaint = user32.EndPaint
EndPaint.argtypes = w.HWND, ct.POINTER(PAINTSTRUCT)
EndPaint.restype = w.BOOL
PostQuitMessage = user32.PostQuitMessage
PostQuitMessage.argtypes = ct.c_int,
PostQuitMessage.restype = None
DefWindowProc = user32.DefWindowProcW
DefWindowProc.argtypes = w.HWND, w.UINT, w.WPARAM, w.LPARAM
DefWindowProc.restype = LRESULT

gdi32 = ct.WinDLL('gdi32', use_last_error=True)
GetStockObject = gdi32.GetStockObject
GetStockObject.argtypes = ct.c_int,
GetStockObject.restype = w.HGDIOBJ

CW_USEDEFAULT = ct.c_int(0x80000000).value
IDI_APPLICATION = MAKEINTRESOURCE(32512)

WS_OVERLAPPED  = 0x00000000
WS_CAPTION     = 0x00C00000
WS_SYSMENU     = 0x00080000
WS_THICKFRAME  = 0x00040000
WS_MINIMIZEBOX = 0x00020000
WS_MAXIMIZEBOX = 0x00010000

WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
assert WS_OVERLAPPEDWINDOW == 0x00CF0000

CS_HREDRAW = 2
CS_VREDRAW = 1

IDC_ARROW = MAKEINTRESOURCE(32512)
WHITE_BRUSH = 0

SW_SHOWNORMAL = 1

WM_PAINT = 15
WM_DESTROY = 2
DT_SINGLELINE = 32
DT_CENTER = 1
DT_VCENTER = 4

def MainWin():
    # Define Window Class
    wndclass = WNDCLASS()
    wndclass.style         = CS_HREDRAW | CS_VREDRAW
    wndclass.lpfnWndProc   = WNDPROC(WndProc)
    wndclass.cbClsExtra    = 0
    wndclass.cbWndExtra    = 0
    wndclass.hInstance     = GetModuleHandle(None)
    wndclass.hIcon         = LoadIcon(None, IDI_APPLICATION)
    wndclass.hCursor       = LoadCursor(None, IDC_ARROW)
    wndclass.hbrBackground = GetStockObject(WHITE_BRUSH)
    wndclass.lpszMenuName  = None
    wndclass.lpszClassName = 'MainWin'

    # Register Window Class
    RegisterClass(ct.byref(wndclass))

    # Create Window
    hwnd = CreateWindowEx(0,
                          wndclass.lpszClassName,
                          'Python Window',
                          WS_OVERLAPPEDWINDOW,
                          CW_USEDEFAULT,
                          CW_USEDEFAULT,
                          CW_USEDEFAULT,
                          CW_USEDEFAULT,
                          None,
                          None,
                          wndclass.hInstance,
                          None)
    # Show Window
    user32.ShowWindow(hwnd, SW_SHOWNORMAL)
    user32.UpdateWindow(hwnd)

    # Pump Messages
    msg = w.MSG()
    while GetMessage(ct.byref(msg), None, 0, 0) != 0:
        TranslateMessage(ct.byref(msg))
        DispatchMessage(ct.byref(msg))

    return msg.wParam

def WndProc(hwnd, message, wParam, lParam):
    ps = PAINTSTRUCT()
    rect = w.RECT()

    if message == WM_PAINT:
        hdc = BeginPaint(hwnd, ct.byref(ps))
        GetClientRect(hwnd, ct.byref(rect))
        DrawText(hdc,
                 'Python Window',
                 -1, ct.byref(rect),
                 DT_SINGLELINE|DT_CENTER|DT_VCENTER)
        EndPaint(hwnd, ct.byref(ps))
        return 0
    elif message == WM_DESTROY:
        PostQuitMessage(0)
        return 0

    return DefWindowProc(hwnd, message, wParam, lParam)

if __name__=='__main__':
    sys.exit(MainWin())