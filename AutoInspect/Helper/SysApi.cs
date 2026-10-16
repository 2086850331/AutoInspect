using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoInspect.Helper
{
    public static class SysApi
    {
        public const int WM_GETTEXT = 0x000D;
        public const int WM_GETTEXTLENGTH = 0x000E;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LVITEM lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        // -------------------- RichTextBox --------------------
        public static IntPtr FindTopLevelWindowByPid(int pid)
        {
            IntPtr found = IntPtr.Zero;
            SysApi.EnumWindows(new SysApi.EnumWindowsProc((IntPtr hWnd, IntPtr lParam) =>
            {
                try
                {
                    if (!SysApi.IsWindowVisible(hWnd)) return true;
                    _ = SysApi.GetWindowTextLength(hWnd);
                    _ = SysApi.GetWindowThreadProcessId(hWnd, out uint windowPid);
                    if ((int)windowPid != pid) return true;

                    found = hWnd;
                    return false;
                }
                catch
                {
                    return true;
                }
            }), IntPtr.Zero);

            return found;
        }

        public static List<IntPtr> FindRichTextBox(IntPtr mainWnd)
        {
            List<IntPtr> result = new();
            SysApi.EnumChildWindows(mainWnd, new SysApi.EnumChildProc((IntPtr hWnd, IntPtr lParam) =>
            {
                var className = new StringBuilder(256);
                _ = SysApi.GetClassName(hWnd, className, className.Capacity);
                string cls = className.ToString();
                if (cls.IndexOf("RichEdit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("RichText", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(hWnd);
                    return true;
                }
                return true;
            }), IntPtr.Zero);
            return result;
        }

        public static string GetRichTextBoxPlainText(IntPtr richText)
        {
            IntPtr lenPtr = SysApi.SendMessage(richText, SysApi.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
            int length = lenPtr.ToInt32();
            if (length <= 0)
                return string.Empty;

            var sb = new StringBuilder(length + 1);
            int copied = SysApi.SendMessage(richText, SysApi.WM_GETTEXT, new IntPtr(sb.Capacity), sb);
            if (copied == 0 && sb.Length == 0)
                return string.Empty;

            return sb.ToString();
        }

        // -------------------- ListView --------------------
        public static List<IntPtr> FindListView(IntPtr mainWnd)
        {
            List<IntPtr> result = new();
            SysApi.EnumChildWindows(mainWnd, new SysApi.EnumChildProc((IntPtr hWnd, IntPtr lParam) =>
            {
                try
                {
                    var className = new StringBuilder(256);
                    _ = SysApi.GetClassName(hWnd, className, className.Capacity);
                    string cls = className.ToString();
                    if (cls.IndexOf("SysListView32", StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Add(hWnd);
                }
                catch { }
                return true;
            }), IntPtr.Zero);
            return result;
        }

        // -------------------- Win32 读取 ListView 列文本 --------------------
        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
        private const int LVM_GETITEMTEXT = LVM_FIRST + 115;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct LVITEM
        {
            public uint mask;
            public int iItem;
            public int iSubItem;
            public uint state;
            public uint stateMask;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pszText;
            public int cchTextMax;
            public int iImage;
            public IntPtr lParam;
            public int iIndent;
            public int iGroupId;
            public uint cColumns;
            public IntPtr puColumns;
            public IntPtr piColFmt;
            public int iGroup;
        }

        private static int GetListViewItemCount(IntPtr listViewHwnd)
        {
            return (int)SendMessage(listViewHwnd, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
        }

        private static string GetListViewItemText(IntPtr listViewHwnd, int row, int col)
        {
            var sb = new StringBuilder(256);
            var lvItem = new LVITEM
            {
                iItem = row,
                iSubItem = col,
                pszText = sb.ToString(),
                cchTextMax = sb.Capacity
            };
            SendMessage(listViewHwnd, LVM_GETITEMTEXT, new IntPtr(row), ref lvItem);
            return lvItem.pszText;
        }

        // -------------------- 替代 System.Windows.Automation 的方法 --------------------
        public static List<string> GetListViewFRemarkTexts(IntPtr listViewHwnd)
        {
            var texts = new List<string>();
            try
            {
                // 假设 F_Remark 列索引固定，这里用 1，如果不固定可改成参数或动态查找
                int fRemarkColumnIndex = 1;

                int rowCount = GetListViewItemCount(listViewHwnd);
                for (int i = 0; i < rowCount; i++)
                {
                    string text = GetListViewItemText(listViewHwnd, i, fRemarkColumnIndex);
                    texts.Add(text);
                }
            }
            catch
            {
                // 失败返回空列表
            }
            return texts;
        }
    }
}
