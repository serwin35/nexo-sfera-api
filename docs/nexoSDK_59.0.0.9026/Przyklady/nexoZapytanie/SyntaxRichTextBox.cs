using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Drawing;

namespace nexoZapytanie
{
    public class SyntaxRichTextBox : System.Windows.Forms.RichTextBox
    {
        [DllImport("user32", EntryPoint = "SendMessageA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern int LockWindowUpdate(int hWnd);

        private DataTable Words = new DataTable();

        private enum EditMessages
        {
            LineIndex = 187,
            LineFromChar = 201,
            GetFirstVisibleLine = 206,
            CharFromPos = 215,
            PosFromChar = 1062
        }

        protected override void OnTextChanged(System.EventArgs e)
        {
            ColorVisibleLines();
        }

        public void ColorRtb()
        {
            int FirstVisibleChar = 0;
            int i = 0;

            while (i < Lines.Length)
            {
                FirstVisibleChar = GetCharFromLineIndex(i);
                ColorLineNumber(i, FirstVisibleChar);
                i += 1;
            }
        }

        public void ColorVisibleLines()
        {
            int FirstLine = FirstVisibleLine();
            int LastLine = LastVisibleLine();
            int FirstVisibleChar = 0;

            if ((FirstLine == 0) && (LastLine == 0))
            {
                return;
            }
            else
            {
                while (FirstLine < LastLine)
                {
                    FirstVisibleChar = GetCharFromLineIndex(FirstLine);
                    ColorLineNumber(FirstLine, FirstVisibleChar);
                    FirstLine += 1;
                }

                ColorComments(FirstLine, LastLine);
            }

        }

        private void ColorComments(int FirstLine, int LastLine)
        {
             //todo
        }

        private char[] separators = new char[] { ' ', ',', ';' };

        public void ColorLineNumber(int LineIndex, int lStart)
        {
            int SelectionAt = SelectionStart;
            DataRow MyRow = null;
            string[] Line = null;
            int MyI = 0;

            LockWindowUpdate(Handle.ToInt32());

            MyI = lStart;

            if (CaseSensitive)
            {
                Line = Lines[LineIndex].Split(separators);
            }
            else
            {
                Line = Lines[LineIndex].ToLower().Split(separators);
            }

            foreach (string MyStr in Line)
            {
                SelectionStart = MyI;
                SelectionLength = MyStr.Length;

                if (Words.Rows.Contains(MyStr))
                {
                    MyRow = Words.Rows.Find(MyStr);
                    if ((!CaseSensitive) || (CaseSensitive && MyRow["Word"].ToString() == MyStr))
                    {
                        SelectionColor = Color.FromName(MyRow["Color"].ToString());
                    }
                }
                else
                {
                    SelectionColor = Color.Black;
                }

                MyI += MyStr.Length + 1;
            }

            SelectionStart = SelectionAt;
            SelectionLength = 0;
            SelectionColor = Color.Black;

            LockWindowUpdate(0);
        }

        public int GetCharFromLineIndex(int LineIndex)
        {
            return SendMessage(Handle, (int)EditMessages.LineIndex, LineIndex, 0);
        }

        public int FirstVisibleLine()
        {
            return SendMessage(Handle, (int)EditMessages.GetFirstVisibleLine, 0, 0);
        }

        public int LastVisibleLine()
        {
            int LastLine = FirstVisibleLine() + (Height / Font.Height);

            if (LastLine > Lines.Length || LastLine == 0)
            {
                LastLine = Lines.Length;
            }

            return LastLine;
        }

        public SyntaxRichTextBox()
        {
            CaseSensitive = false;

            AcceptsTab = true;

            FillTableWithWords(arrKeyWordsBlue, Color.Blue.Name);
            FillTableWithWords(arrKeyWordsPurple, Color.Purple.Name);
            FillTableWithWords(arrKeyWordsGreen, Color.Green.Name);
            FillTableWithWords(arrKeyWordsGray, Color.Gray.Name);

            VisibleChanged += new EventHandler(SyntaxRichTextBox_VisibleChanged);
        }

        void SyntaxRichTextBox_VisibleChanged(object sender, EventArgs e)
        {
            if(Visible)
                ColorVisibleLines();
        }


        private void FillTableWithWords(string[] arrKeyWords, string strColor)
        {
            if (Words.Columns.Count == 0)
            {
                Words.Columns.Add("Word");
                Words.PrimaryKey = new DataColumn[] { Words.Columns[0] };
                Words.Columns.Add("Color");
            }

            foreach (string strKW in arrKeyWords)
            {
                var row = Words.NewRow();
                row["Word"] = strKW;
                row["Color"] = strColor;
                if(Words.Select(string.Format("Word='{0}'", strKW)).Length == 0)
                {
                    Words.Rows.Add(row);
                }
                else
                    Debug.WriteLine(strKW);
            }
        }
        public bool CaseSensitive { get; set; }

        static string[] arrKeyWordsBlue = new string[] {
			    "select",
			    "insert",
			    "delete",
			    "truncate",
			    "from",
			    "where",
			    "into",
			    "inner",
			    "update",
			    "outer",
			    "on",
			    "is",
			    "declare",
			    "set",
			    "use",
			    "values",
			    "as",
			    "order",
			    "by",
			    "drop",
			    "view",
			    "go",
			    "goto",
			    "trigger",
			    "cube",
			    "binary",
			    "varbinary",
			    "image",
			    "char",
			    "varchar",
			    "text",
			    "datetime",
			    "smalldatetime",
			    "decimal",
			    "numeric",
			    "float",
			    "real",
			    "bigint",
			    "int",
			    "smallint",
			    "tinyint",
			    "money",
			    "smallmoney",
			    "bit",
			    "cursor",
			    "timestamp",
			    "uniqueidentifier",
			    "sql_variant",
			    "table",
			    "nchar",
			    "nvarchar",
			    "ntext",
			    "like",
			    "and",
			    "all",
			    "in",
			    "null",
			    "join",
			    "not",
			    "or",
                "case",
                "identity",
                "rowcount_big",
                "setuser",
                "containstable",
                "openquery",
                "freetexttable",
                "openrowset",
                "opendatasource",
                "openxml",
		    };

        static string[] arrKeyWordsPurple = new string[] {
                "patindex",
                "textvalid",
                "textptr",
                "@@connections",
                "@@pack_received",
                "@@cpu_busy",
                "@@pack_sent",
                "@@timeticks",
                "@@idle",
                "@@total_errors",
                "@@io_busy",
                "@@total_read",
                "@@packet_errors",
                "@@total_write",
                "app_name",
                "cast",
                "convert",
                "coalesce",
                "collationproperty",
                "columns_updated",
                "current_timestamp",
                "datalength",
                "@@error",
                "error_line",
                "error_message",
                "error_number",
                "error_procedure",
                "error_severity",
                "error_state",
                "formatmessage",
                "getansinull",
                "host_id",
                "host_name",
                "ident_current",
                "ident_incr",
                "ident_seed",
                "@@identity",
                "isdate",
                "isnull",
                "isnumeric",
                "newid",
                "nullif",
                "parsename",
                "original_login",
                "@@rowcount",
                "scope_identity",
                "serverproperty",
                "sessionproperty",
                "session_user",
                "stats_date",
                "system_user",
                "@@trancount",
                "user_name",
                "xact_state",
                "ascii",
                "soundex",
                "space",
                "charindex",
                "quotename",
                "str",
                "difference",
                "replace",
                "stuff",
                "replicate",
                "substring",
                "len",
                "reverse",
                "unicode",
                "lower",
                "upper",
                "ltrim",
                "rtrim",
                "current_user",
                "suser_id",
                "has_perms_by_name",
                "suser_sid",
                "is_member",
                "suser_sname",
                "is_srvrolemember",
                "permissions",
                "suser_name",
                "user_id",
                "rank",
                "ntile",
                "dense_rank",
                "row_number",
                "@@procid",
                "assemblyproperty",
                "fulltextcatalogproperty",
                "col_length",
                "fulltextserviceproperty",
                "col_name",
                "index_col",
                "columnproperty",
                "indexkey_property",
                "databaseproperty",
                "indexproperty",
                "databasepropertyex",
                "object_id",
                "db_id",
                "object_name",
                "db_name",
                "objectproperty",
                "file_id",
                "objectpropertyex",
                "file_idex",
                "schema_id",
                "file_name",
                "schema_name",
                "filegroup_id",
                "sql_variant_property",
                "filegroup_name",
                "type_id",
                "filegroupproperty",
                "type_name",
                "fileproperty",
                "typeproperty",
                "abs",
                "degrees",
                "rand",
                "acos",
                "exp",
                "round",
                "asin",
                "floor",
                "sign",
                "atan",
                "log",
                "sin",
                "atn2",
                "log10",
                "sqrt",
                "ceiling",
                "pi",
                "square",
                "cos",
                "power",
                "tan",
                "cot",
                "radians",
                "dateadd",
                "datediff",
                "datename",
                "datepart",
                "day",
                "getdate",
                "getutcdate",
                "month",
                "year",
                "@@cursor_rows",
                "cursor_status",
                "@@fetch_status",
                "@@datefirst",
                "@@options",
                "@@dbts",
                "@@remserver",
                "@@langid",
                "@@servername",
                "@@language",
                "@@servicename",
                "@@lock_timeout",
                "@@spid",
                "@@max_connections",
                "@@textsize",
                "@@max_precision",
                "@@version",
                "@@nestlevel",
                "avg",
                "min",
                "checksum_agg",
                "sum",
                "count",
                "stdev",
                "count_big",
                "stdevp",
                "grouping",
                "var",
                "max",
                "varp",
            };
        static string[] arrKeyWordsGreen = new string[] {
                "fn_virtualfilestats",
                "fn_helpcollations",
                "fn_servershareddrives",
                "sys.dm_db_index_physical_stats",
                "sys.fn_builtin_permissions",
                "fn_listextendedproperty",

            };
        static string[] arrKeyWordsGray = new string[] {
                "left",
                "right",
            };

    }

}
