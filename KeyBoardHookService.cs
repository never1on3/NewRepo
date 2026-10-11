using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SecretBoardKey
{
    public class KeyBoardHookService
    {
        // Константы Win32 API
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint LLKHF_INJECTED = 0x00000010;

        // Делегат и структуры
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // P/Invoke
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // Переменные состояния
        private LowLevelKeyboardProc _hookDelegate;
        private IntPtr _hookID = IntPtr.Zero;

        // Карта замен
        private readonly Dictionary<uint, string> _kbdMap = new Dictionary<uint, string>
        {
            { 0x46, ":-" },   { 0xBC, "/)" },   { 0x4C, "/]" },   { 0x54, "!:" },
            { 0xC0, "#" },    { 0xBA, "[}" },   { 0x50, "+)" },   { 0x42, "=" },
            { 0x51, "^" },    { 0x52, "|{" },   { 0x4B, "✓" },    { 0x45, "√" },
            { 0x4A, "•" },    { 0x47, "/|" },   { 0x48, "|`" },   { 0x56, "π" },
            { 0x59, "|+" },   { 0x41, "{}" },   { 0x57, "|/" },   { 0xDB, "*" },
            { 0x58, "4" },    { 0x49, "|+2" },  { 0x4F, "|+2/" }, { 0xDD, "¶•" },
            { 0x53, "|°" },   { 0x4D, "¶" },    { 0xDE, "¢" },    { 0xBE, "|0" },
            { 0x5A, "₹" },    { 0x43, "{-" },   { 0x4E, "|" },    { 0x44, "(+" },
            { 0x55, "_/" }
        };

        // Запуск перехвата
        public bool Start()
        {
            if (_hookID != IntPtr.Zero) return true; // Ужe запущен

            _hookDelegate = HookCallback;
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _hookDelegate, GetModuleHandle(curModule.ModuleName), 0);
            }

            return _hookID != IntPtr.Zero;
        }

        // Остановка перехвата
        public void Stop()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT pKeyStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                // Физическое нажатие
                if ((pKeyStruct.flags & LLKHF_INJECTED) == 0)
                {
                    IntPtr hwnd = GetForegroundWindow();
                    uint threadId = GetWindowThreadProcessId(hwnd, out _);
                    IntPtr currentLayout = GetKeyboardLayout(threadId);

                    // Русская раскладка (0x0419)
                    if ((ushort)((long)currentLayout & 0xFFFF) == 0x0419)
                    {
                        if (_kbdMap.TryGetValue(pKeyStruct.vkCode, out string replacementStr))
                        {
                            CustomSendString(replacementStr);
                            return (IntPtr)1; // Подавляем нажатие
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void CustomSendString(string str)
        {
            List<INPUT> inputs = new List<INPUT>();

            foreach (char ch in str)
            {
                INPUT downInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE }
                    }
                };
                inputs.Add(downInput);

                INPUT upInput = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP }
                    }
                };
                inputs.Add(upInput);
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
