/*
 * Copyright 2014 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : ScadaWebCommon
 * Summary  : Retrieve data from the configuration database, snapshot tables and events
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2005
 * Modified : 2014
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using Scada.Client;
using Scada.Data;
using Utils;

namespace Scada.Web
{
	/// <summary>
    /// Retrieve data from the configuration database, snapshot tables and events
    /// <para>��������� ������ �� ���� ������������, ������ ������ � �������</para>
	/// </summary>
	public class MainData
	{
        /// <summary>
        /// ������� � ������� ��� ����������� �����
        /// </summary>
        public class EventView
        {
            /// <summary>
            /// �����������
            /// </summary>
            public EventView()
            {
                Num = "";
                Date = "";
                Time = "";
                Obj = "";
                KP = "";
                Cnl = "";
                Text = "";
                Check = false;
                User = "";
                Color = "black";
                Sound = false;
            }

            /// <summary>
            /// �������� ��� ���������� ���������� ����� ������� � �����
            /// </summary>
            public string Num { get; set; }
            /// <summary>
            /// �������� ��� ���������� ���� �������
            /// </summary>
            public string Date { get; set; }
            /// <summary>
            /// �������� ��� ���������� ����� �������
            /// </summary>
            public string Time { get; set; }
            /// <summary>
            /// �������� ��� ���������� ������
            /// </summary>
            public string Obj { get; set; }
            /// <summary>
            /// �������� ��� ���������� ������
            /// </summary>
            public string KP { get; set; }
            /// <summary>
            /// �������� ��� ���������� �����
            /// </summary>
            public string Cnl { get; set; }
            /// <summary>
            /// �������� ��� ���������� ����� �������
            /// </summary>
            public string Text { get; set; }
            /// <summary>
            /// �������� ��� ���������� ������� ������������
            /// </summary>
            public bool Check { get; set; }
            /// <summary>
            /// �������� ��� ���������� ��� ������������, �������������� �������
            /// </summary>
            public string User { get; set; }
            /// <summary>
            /// �������� ��� ���������� ����
            /// </summary>
            public string Color { get; set; }
            /// <summary>
            /// �������� ��� ���������� ������� ����� �������
            /// </summary>
            public bool Sound { get; set; }
        }

        /// <summary>
        /// ����� �� ������ � �������� ����������
        /// </summary>
        public struct Right
        {
            /// <summary>
            /// ���������� ����
            /// </summary>
            public static readonly Right NoRights = new Right(false, false);

            /// <summary>
            /// �����������
            /// </summary>
            public Right(bool viewRight, bool ctrlRight)
                : this()
            {
                ViewRight = viewRight;
                CtrlRight = ctrlRight;
            }

            /// <summary>
            /// �������� ��� ���������� ����� �� ��������
            /// </summary>
            public bool ViewRight { get; set; }
            /// <summary>
            /// �������� ��� ���������� ����� �� ����������
            /// </summary>
            public bool CtrlRight { get; set; }
        }


		/// <summary>
        /// ����� ������������ ������ ���� ������������, �
		/// </summary>
		public const int BaseValidTime = 1;
		/// <summary>
		/// ����� ������������ ������ �������� �����, �
		/// </summary>
		public const int CurSrezValidTime = 1;
		/// <summary>
		/// ����� ����������� ������ �������� �����, ���
		/// </summary>
		public const int CurSrezShowTime = 15;
		/// <summary>
		/// ����� ������������ ������ ������� ������, �
		/// </summary>
		public const int HourSrezValidTime = 5;
		/// <summary>
		/// ����� ������������ ������ �������� ������, �
		/// </summary>
		public const int MinSrezValidTime = 5;
        /// <summary>
        /// ����� ������������ ������ �������, �
        /// </summary>
        public const int EventValidTime = 1;

        /// <summary>
        /// ������ ������ ������ ������� ������ ��� �����������
        /// </summary>
        public const int HourCacheSize = 5;
        /// <summary>
        /// ������ ������ ������ ������� ��� �����������
        /// </summary>
        public const int EventCacheSize = 5;


        ServerComm serverComm;           // ������ ��� ������ ������� �� SCADA-��������
        private string settFileName;     // ������ ��� ����� �������� ���������� �� SCADA-��������
        private DateTime settModTime;    // ����� ���������� ��������� ����� ��������

        private SrezTableLight tblCur;             // ������� �������� �����
        private SrezTableLight[] hourTableCache;   // ������ ������ ������� ������ ��� �����������
        private EventTableLight[] eventTableCache; // ������ ������ ������� ��� �����������
        private int hourTableIndex;                // ��������� ������ ��� ���������� ������ ������� ������ � ���
        private int eventTableIndex;               // ��������� ������ ��� ���������� ������ ������� � ���
        private Trend trend;                       // ��������� ���������� �������� �����
        private NumberFormatInfo nfi;              // ������ ������������ �����
        private string defDecSep;                  // ����������� ������� ����� �� ���������
        private string defGrSep;                   // ����������� ����� ���� �� ���������

        private DateTime baseModTime;    // ����� ���������� ��������� ������� ��������� ���� ������������
        private DateTime baseFillTime;   // ����� ��������� ��������� ���������� ������ ���� ������������
		private DataTable tblInCnl;      // ������� ������� �������
        private DataTable tblCtrlCnl;    // ������� ������� ����������
        private DataTable tblObj;        // ������� ��������
        private DataTable tblKP;         // ������� ��
        private DataTable tblRole;       // ������� �����
        private DataTable tblUser;       // ������� �������������
        private DataTable tblInterface;  // ������� �������� ����������
        private DataTable tblRight;      // ������� ���� �� ������� ����������
        private DataTable tblEvType;     // ������� ����� �������
        private DataTable tblParam;      // ������� ����������
		private DataTable tblUnit;       // ������� ������������
        private DataTable tblCmdVal;     // ������� �������� ������
        private DataTable tblFormat;     // ������� �������� �����
        private DataTable[] baseTblArr;  // ������ ������ �� ������� ���� ������������

		private CnlProps[] cnlPropsArr;  // ������ ������� ������� �������
        private int maxCnlCnt;           // ������������ ���������� ������� �������

        private Object refrLock;         // ������ ��� ������������� ���������� ������
        private Object baseLock;         // ������ ��� ������������� ��������� � �������� ���� ������������
        private Object cnlPropLock;      // ������ ��� ������������� ��������� ������� �������� ������
        private Object cnlDataLock;      // ������ ��� ������������� ��������� ������ �������� ������
        private Object eventLock;        // ������ ��� ������������� ��������� �������
        

        /// <summary>
        /// �����������
        /// </summary>
		public MainData()
		{
            serverComm = null;
            settFileName = "";
            settModTime = DateTime.MinValue;

            tblCur = new SrezTableLight();
            hourTableCache = new SrezTableLight[HourCacheSize];
            eventTableCache = new EventTableLight[EventCacheSize];
            for (int i = 0; i < HourCacheSize; i++)
                hourTableCache[i] = null;
            for (int i = 0; i < EventCacheSize; i++)
                eventTableCache[i] = null;
            hourTableIndex = 0;
            eventTableIndex = 0;
            trend = null;
            nfi = new NumberFormatInfo();
            defDecSep = Localization.Culture.NumberFormat.NumberDecimalSeparator;
            defGrSep = Localization.Culture.NumberFormat.NumberGroupSeparator;

            baseModTime = DateTime.MinValue;
            baseFillTime = DateTime.MinValue;
            tblInCnl = new DataTable("InCnl");
            tblCtrlCnl = new DataTable("CtrlCnl");
            tblObj = new DataTable("Obj");
            tblKP = new DataTable("KP");
            tblRole = new DataTable("Role");
            tblUser = new DataTable("User");
            tblInterface = new DataTable("Interface");
            tblRight = new DataTable("Right");
            tblEvType = new DataTable("EvType");
            tblParam = new DataTable("Param");
            tblUnit = new DataTable("Unit");
            tblCmdVal = new DataTable("CmdVal");
            tblFormat = new DataTable("Format");

            baseTblArr = new DataTable[13];
            baseTblArr[0] = tblInCnl;
            baseTblArr[1] = tblCtrlCnl;
            baseTblArr[2] = tblObj;
            baseTblArr[3] = tblKP;
            baseTblArr[4] = tblRole;
            baseTblArr[5] = tblUser;
            baseTblArr[6] = tblInterface;
            baseTblArr[7] = tblRight;
            baseTblArr[8] = tblEvType;
            baseTblArr[9] = tblParam;
            baseTblArr[10] = tblUnit;
            baseTblArr[11] = tblCmdVal;
            baseTblArr[12] = tblFormat;

			cnlPropsArr = null;
            maxCnlCnt = 0;

            refrLock = new Object();
            baseLock = new Object();
            cnlPropLock = new Object();
            cnlDataLock = new Object();
            eventLock = new Object();
        }


        /// <summary>
        /// �������� ������ ��� ������ ������� �� SCADA-��������
        /// </summary>
        public ServerComm ServerComm
        {
            get
            {
                RefrServerComm();
                return serverComm;
            }
        }

        /// <summary>
        /// ��� ����� �������� ���������� �� SCADA-��������
        /// </summary>
        public string SettingsFileName
        {
            get
            {
                return settFileName;
            }
            set
            {
                settFileName = value;
            }
        }

		/// <summary>
		/// ������ ������� ������� �������
		/// </summary>
		public CnlProps[] CnlPropsArr
		{
			get
			{
				return cnlPropsArr;
			}
		}


        /// <summary>
		/// ���� � ����� ��������� ������ � ����, ��� ���������� ����� - ����������� ����
		/// </summary>
		private DateTime GetLastWriteTime(string path)
		{
            if (!File.Exists(path))
                return DateTime.MinValue;

			try
			{
				return File.GetLastWriteTime(path);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		/// <summary>
		/// ��������� �������� ������� �������
		/// </summary>
		private void FillCnlProps()
		{
            Monitor.Enter(baseLock);
            AppData.Log.WriteAction(Localization.UseRussian ? "���������� ������� ������� �������" : 
                "Fill input channels properties", Log.ActTypes.Action);

            try
            {
                int inCnlCnt = tblInCnl.Rows.Count; // ���������� ������� �������

                if (inCnlCnt == 0)
                {
                    cnlPropsArr = null;
                }
                else
                {
                    if (0 < maxCnlCnt && maxCnlCnt < inCnlCnt)
                        inCnlCnt = maxCnlCnt;
                    CnlProps[] newCnlPropsArr = new CnlProps[inCnlCnt];

                    for (int i = 0; i < inCnlCnt; i++)
                    {
                        DataRowView rowView = tblInCnl.DefaultView[i];
                        int cnlNum = (int)rowView["CnlNum"];
                        CnlProps cnlProps = GetCnlProps(cnlNum);
                        if (cnlProps == null) 
                            cnlProps = new CnlProps(cnlNum);

                        // ����������� �������, �� ������������ ������� ������
                        cnlProps.CnlName = (string)rowView["Name"];
                        cnlProps.CtrlCnlNum = (int)rowView["CtrlCnlNum"];
                        cnlProps.EvSound = (bool)rowView["EvSound"];

                        // ����������� ������ � ������������ �������
                        cnlProps.ObjNum = (int)rowView["ObjNum"];
                        tblObj.DefaultView.RowFilter = "ObjNum = " + cnlProps.ObjNum;
                        cnlProps.ObjName = tblObj.DefaultView.Count > 0 ? (string)tblObj.DefaultView[0]["Name"] : "";

                        // ����������� ������ � ������������ ��
                        cnlProps.KPNum = (int)rowView["KPNum"];
                        tblKP.DefaultView.RowFilter = "KPNum = " + cnlProps.KPNum;
                        cnlProps.KPName = tblKP.DefaultView.Count > 0 ? (string)tblKP.DefaultView[0]["Name"] : "";

                        // ����������� ������������ ��������� � ����� ����� ������
                        tblParam.DefaultView.RowFilter = "ParamID = " + rowView["ParamID"];
                        if (tblParam.DefaultView.Count > 0)
                        {
                            DataRowView paramRowView = tblParam.DefaultView[0];
                            cnlProps.ParamName = (string)paramRowView["Name"];
                            object iconFileName = paramRowView["IconFileName"];
                            cnlProps.IconFileName = iconFileName == DBNull.Value ? "" : iconFileName.ToString();
                        }
                        else
                        {
                            cnlProps.ParamName = "";
                            cnlProps.IconFileName = "";
                        }

                        // ����������� ������� ������
                        tblFormat.DefaultView.RowFilter = "FormatID = " + rowView["FormatID"];
                        if (tblFormat.DefaultView.Count > 0)
                        {
                            DataRowView formatRowView = tblFormat.DefaultView[0];
                            cnlProps.ShowNumber = (bool)formatRowView["ShowNumber"];
                            cnlProps.DecDigits = (int)formatRowView["DecDigits"];
                        }

                        // ����������� ������������
                        tblUnit.DefaultView.RowFilter = "UnitID = " + rowView["UnitID"];
                        if (tblUnit.DefaultView.Count > 0)
                        {
                            string sign = (string)tblUnit.DefaultView[0]["Sign"];
                            cnlProps.UnitArr = sign.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < cnlProps.UnitArr.Length; j++)
                                cnlProps.UnitArr[j] = cnlProps.UnitArr[j].Trim();
                            if (cnlProps.UnitArr.Length == 1 && cnlProps.UnitArr[0] == "")
                                cnlProps.UnitArr = null;
                        }
                        else
                        {
                            cnlProps.UnitArr = null;
                        }

                        newCnlPropsArr[i] = cnlProps;
                    }

                    cnlPropsArr = newCnlPropsArr;
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ���������� ������� ������� �������: " :
                    "Error filling input channels properties: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(baseLock);
            }
        }

        /// <summary>
        /// �������� (�����������) ������ ��� ������ ������� �� SCADA-��������
        /// ��� ��������� ����� �������� ���������� �� SCADA-��������
        /// </summary>
        private void RefrServerComm()
        {
            if (settFileName != "")
            {
                DateTime dateTime = GetLastWriteTime(settFileName);
                if (dateTime > DateTime.MinValue && dateTime != settModTime)
                {
                    settModTime = dateTime;
                    CommSettings commSettings = new CommSettings();
                    commSettings.LoadFromFile(settFileName, AppData.Log);
                    if (serverComm == null || !serverComm.CommSettings.Equals(commSettings))
                    {
                        if (serverComm != null)
                        {
                            serverComm.Close();

                            tblCur = new SrezTableLight();
                            for (int i = 0; i < HourCacheSize; i++)
                                hourTableCache[i] = null;
                            for (int i = 0; i < EventCacheSize; i++)
                                eventTableCache[i] = null;
                            hourTableIndex = 0;
                            eventTableIndex = 0;
                            trend = null;

                            baseModTime = DateTime.MinValue;
                            baseFillTime = DateTime.MinValue;
                        }
                        serverComm = new ServerComm(commSettings);
                    }
                }
            }
        }

        /// <summary>
        /// ������������� ������� � ������� ��� ����������� �����
        /// </summary>
        private EventView ConvEvent(EventTableLight.Event ev)
        {
            EventView eventView = new EventView();

            eventView.Num = ev.Number.ToString();
            eventView.Date = ev.DateTime.ToString("d", Localization.Culture);
            eventView.Time = ev.DateTime.ToString("T", Localization.Culture);
            eventView.Text = ev.Descr;

            // ��������� ������� ������ �������
            CnlProps cnlProps = GetCnlProps(ev.CnlNum);

            // ����������� ������������ �������
            if (cnlProps == null || cnlProps.ObjNum != ev.ObjNum)
            {
                tblObj.DefaultView.RowFilter = "ObjNum = " + ev.ObjNum;
                if (tblObj.DefaultView.Count > 0)
                    eventView.Obj = (string)tblObj.DefaultView[0]["Name"];
            }
            else
            {
                eventView.Obj = cnlProps.ObjName;
            }

            // ����������� ������������ ��
            if (cnlProps == null || cnlProps.KPNum != ev.KPNum)
            {
                tblKP.DefaultView.RowFilter = "KPNum = " + ev.KPNum;
                if (tblKP.DefaultView.Count > 0)
                    eventView.KP = (string)tblKP.DefaultView[0]["Name"];
            }
            else
            {
                eventView.KP = cnlProps.KPName;
            }

            if (cnlProps != null)
            {
                // ����������� ������������ ������ � �������� �����
                eventView.Cnl = cnlProps.CnlName;
                eventView.Sound = cnlProps.EvSound;

                // �������� ������ ������� ������
                int newCnlStat = ev.NewCnlStat;
                bool newValIsUndef = newCnlStat <= BaseValues.ParamStat.Undefined ||
                    newCnlStat == BaseValues.ParamStat.FormulaError || newCnlStat == BaseValues.ParamStat.Unreliable;

                // ����������� �����
                if (!cnlProps.ShowNumber && cnlProps.UnitArr != null && cnlProps.UnitArr.Length == 2)
                {
                    if (!newValIsUndef)
                        eventView.Color = ev.NewCnlVal > 0 ? "green" : "red";
                }
                else
                {
                    string color;
                    if (GetColorByStat(newCnlStat, out color))
                        eventView.Color = color;
                }

                // ����������� ������ �������, ���� �� ������ ��� ��������
                if (eventView.Text == "")
                {
                    // ��������� ���� �������
                    tblEvType.DefaultView.RowFilter = "CnlStatus = " + newCnlStat;
                    string evTypeName = tblEvType.DefaultView.Count > 0 ? 
                        (string)tblEvType.DefaultView[0]["Name"] : "";

                    if (newValIsUndef)
                    {
                        eventView.Text = evTypeName;
                    }
                    else if (cnlProps.ShowNumber)
                    {
                        // ���������� ���� �������
                        if (evTypeName != "")
                            eventView.Text = evTypeName + ": ";
                        // ���������� �������� ������
                        nfi.NumberDecimalDigits = cnlProps.DecDigits;
                        nfi.NumberDecimalSeparator = defDecSep;
                        nfi.NumberGroupSeparator = defGrSep;
                        eventView.Text += ev.NewCnlVal.ToString("N", nfi);
                        // ���������� �����������
                        if (cnlProps.UnitArr != null)
                            eventView.Text += " " + cnlProps.UnitArr[0];
                    }
                    else if (cnlProps.UnitArr != null)
                    {
                        int unitInd = (int)ev.NewCnlVal;
                        if (unitInd < 0) 
                            unitInd = 0;
                        else if (unitInd >= cnlProps.UnitArr.Length) 
                            unitInd = cnlProps.UnitArr.Length - 1;
                        eventView.Text = cnlProps.UnitArr[unitInd];
                    }
                }
            }

            // ����������� ������� ������������
            eventView.Check = ev.Checked;

            if (ev.Checked)
            {
                tblUser.DefaultView.RowFilter = "UserID = " + ev.UserID;
                eventView.User = tblUser.DefaultView.Count > 0 ? (string)tblUser.DefaultView[0]["Name"] : WebPhrases.EventChecked;
            }
            else
            {
                eventView.User = WebPhrases.EventUnchecked;
            }

            return eventView;
        }

        /// <summary>
        /// �������� ����, ��������������� �������
        /// </summary>
        private bool GetColorByStat(int stat, out string color)
        {
            if (tblEvType.Columns.Count > 0) // ������� ���������
            {
                tblEvType.DefaultView.RowFilter = "CnlStatus = " + stat;
                if (tblEvType.DefaultView.Count > 0)
                {
                    object colorObj = tblEvType.DefaultView[0]["Color"];
                    if (colorObj != DBNull.Value)
                    {
                        color = colorObj.ToString();
                        return true;
                    }
                }
            }

            color = "";
            return false;
        }
		

		/// <summary>
        /// �������� ������� ���� ������������, ���� ��� ����������
		/// </summary>
		public void RefreshBase()
		{
            Monitor.Enter(refrLock);
            DateTime nowDT = DateTime.Now;

            try
            {
                // ���������� ������� ��� ������ ������� �� SCADA-��������
                RefrServerComm();

                // ���������� ���� ������������
                DateTime inCnlsModTime = serverComm.ReceiveFileAge(ServerComm.Dirs.BaseDAT, tblInCnl.TableName + ".dat");

                if ((nowDT - baseFillTime).TotalSeconds > BaseValidTime /*������ ��������*/ &&
                    inCnlsModTime != baseModTime /*���� ������� ������� ������� ������*/ &&
                    inCnlsModTime > DateTime.MinValue)
                {
                    AppData.Log.WriteAction(Localization.UseRussian ? "���������� ������ ���� ������������" :
                        "Refresh tables of the configuration database", Log.ActTypes.Action);
                    baseModTime = inCnlsModTime;
                    baseFillTime = nowDT;

                    // �������� ���������� ������ SCADA-��������
                    try
                    {
                        DateTime t0 = nowDT;
                        TimeSpan waitSpan = new TimeSpan(0, 0, 5); // 5 ������
                        while (serverComm.ReceiveFileAge(ServerComm.Dirs.BaseDAT, "baselock") > DateTime.MinValue &&
                            DateTime.Now - t0 < waitSpan)
                            Thread.Sleep(500);
                    }
                    catch
                    {
                    }

                    // ���������� ������
                    Monitor.Enter(baseLock);
                    try
                    {
                        for (int i = 0; i < baseTblArr.Length; i++)
                        {
                            DataTable dataTable = baseTblArr[i];
                            if (!serverComm.ReceiveBaseTable(dataTable.TableName + ".dat", dataTable))
                                baseModTime = DateTime.MinValue;
                        }
                    }
                    finally
                    {
                        Monitor.Exit(baseLock);
                    }

                    // ���������� ������� ������� �������
                    FillCnlProps();
                }
            }
            catch (Exception ex)
            {
                baseModTime = DateTime.MinValue;
                baseFillTime = DateTime.MinValue;

                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ���������� ������ ���� ������������: " :
                    "Error refreshing tables of the configuration database: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(refrLock);
            }
		}
        
        /// <summary>
        /// �������� ������� ���� ������������ � �������� �����, ���� ��� ����������
        /// </summary>
        public void RefreshData()
        {
            SrezTableLight hourTable;
            RefreshData(DateTime.MinValue, out hourTable);
        }

        /// <summary>
        /// �������� ������� ���� ������������, �������� � ������� ������, ���� ��� ����������
        /// </summary>
        /// <param name="reqDate">���� ������������� ������� ������</param>
        /// <param name="hourTable">������� ������� ������</param>
		public void RefreshData(DateTime reqDate, out SrezTableLight hourTable)
		{
			RefreshBase();
            Monitor.Enter(refrLock);

            try
            {
                // ���������� �������� �����
                DateTime now = DateTime.Now;

                if ((now - tblCur.LastFillTime).TotalSeconds > CurSrezValidTime) // ������ ��������
                {
                    DateTime curModTime = serverComm.ReceiveFileAge(ServerComm.Dirs.Cur, "current.dat");
                    if (curModTime != tblCur.FileModTime) // ���� ����� ������
                        tblCur.FileModTime = serverComm.ReceiveSrezTable("current.dat", tblCur) ?
                            curModTime : DateTime.MinValue;
                }

                // ���������� ������� ������� ������
                hourTable = null;

                if (reqDate > DateTime.MinValue)
                {
                    string hourTableName = "h" + reqDate.ToString("yyMMdd") + ".dat";

                    // ����� ������� ������� ������� ������ � ����
                    int tableIndex = -1;
                    for (int i = 0; i < HourCacheSize; i++)
                    {
                        hourTable = hourTableCache[i];
                        if (hourTable != null && hourTable.TableName == hourTableName)
                        {
                            tableIndex = i;
                            break;
                        }
                    }

                    if (tableIndex < 0 || (now - hourTable.LastFillTime).TotalSeconds > HourSrezValidTime /*������ ��������*/)
                    {
                        DateTime fileModTime = serverComm.ReceiveFileAge(ServerComm.Dirs.Hour, hourTableName);

                        if (tableIndex < 0)
                        {
                            hourTable = null;

                            // ����������� ����� � ���� ��� ����� ������� ������� ������
                            tableIndex = hourTableIndex;
                            if (++hourTableIndex == HourCacheSize)
                                hourTableIndex = 0;
                        }

                        if (hourTable == null || fileModTime != hourTable.FileModTime /*���� ������ ������*/)
                        {
                            // �������� ����� ������� ������� ������
                            hourTable = new SrezTableLight();
                            hourTableCache[tableIndex] = hourTable;

                            // �������� ������� ������� ������
                            if (serverComm.ReceiveSrezTable(hourTableName, hourTable))
                                hourTable.FileModTime = fileModTime;
                        }
                    }
                }
            }
            finally
            {
                Monitor.Exit(refrLock);
            }
		}

        /// <summary>
        /// �������� ������� ���� ������������ � �������, ���� ��� ����������
        /// </summary>
        /// <param name="reqDate">���� ������������� �������</param>
        /// <param name="eventTable">������� �������</param>
        public void RefreshEvents(DateTime reqDate, out EventTableLight eventTable)
        {
            RefreshBase();
            Monitor.Enter(refrLock);

            try
            {
                // ���������� ������� �������
                string eventTableName = "e" + reqDate.ToString("yyMMdd") + ".dat";
                eventTable = null;

                // ����� ������� ������� ������� � ����
                int tableIndex = -1;
                for (int i = 0; i < EventCacheSize; i++)
                {
                    eventTable = eventTableCache[i];
                    if (eventTable != null && eventTable.TableName == eventTableName)
                    {
                        tableIndex = i;
                        break;
                    }
                }

                if (tableIndex < 0 || (DateTime.Now - eventTable.LastFillTime).TotalSeconds > EventValidTime /*������ ��������*/)
                {
                    DateTime fileModTime = serverComm.ReceiveFileAge(ServerComm.Dirs.Events, eventTableName);

                    if (tableIndex < 0)
                    {
                        eventTable = null;

                        // ����������� ����� � ���� ��� ����� ������� �������
                        tableIndex = eventTableIndex;
                        if (++eventTableIndex == EventCacheSize)
                            eventTableIndex = 0;
                    }

                    if (eventTable == null || fileModTime != eventTable.FileModTime /*���� ������� ������*/)
                    {
                        // �������� ����� ������� �������
                        eventTable = new EventTableLight();
                        eventTableCache[tableIndex] = eventTable;

                        // �������� ������� ������� ������
                        if (serverComm.ReceiveEventTable(eventTableName, eventTable))
                            eventTable.FileModTime = fileModTime;
                    }
                }
            }
            finally
            {
                Monitor.Exit(refrLock);
            }
        }

        /// <summary>
        /// ���������� ���������� ������������ ������� �������
        /// </summary>
        /// <param name="maxCnlCnt">������������ ���������� ������� ������� ��� 0, ���� ���������� ������������</param>
        public void RestrictCnlCnt(int maxCnlCnt)
        {
            Monitor.Enter(cnlPropLock);
            try
            {
                this.maxCnlCnt = maxCnlCnt;
                baseModTime = DateTime.MinValue;
                baseFillTime = DateTime.MinValue;
            }
            finally
            {
                Monitor.Exit(cnlPropLock);
            }
        }


		/// <summary>
		/// �������� �������� �������� ������ �� ��� ������
		/// </summary>
		public CnlProps GetCnlProps(int cnlNum)
		{
            Monitor.Enter(cnlPropLock);
            CnlProps cnlProps = null;

            try
            {                
                if (cnlPropsArr != null)
                {
                    int ind = Array.BinarySearch(cnlPropsArr, (object)cnlNum);
                    if (ind >= 0)
                        cnlProps = cnlPropsArr[ind];
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction(string.Format(Localization.UseRussian ? 
                    "������ ��� ��������� ������� �������� ������ {0}: {1}" : 
                    "Error getting input channel {0} properties: {1}", cnlNum, ex.Message), Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(cnlPropLock);
            }

            return cnlProps;
		}

        /// <summary>
        /// �������� �������� ������ ���������� �� ��� ������
        /// </summary>
        public CtrlCnlProps GetCtrlCnlProps(int ctrlCnlNum)
        {
            Monitor.Enter(baseLock);
            CtrlCnlProps ctrlCnlProps = null;

            try
            {
                tblCtrlCnl.DefaultView.RowFilter = "CtrlCnlNum = " + ctrlCnlNum;
                if (tblCtrlCnl.DefaultView.Count > 0)
                {
                    DataRowView rowView = tblCtrlCnl.DefaultView[0];
                    ctrlCnlProps = new CtrlCnlProps(ctrlCnlNum);
                    ctrlCnlProps.CtrlCnlName = (string)rowView["Name"];
                    ctrlCnlProps.CmdTypeID = (int)rowView["CmdTypeID"];

                    // ����������� ������ � ������������ �������
                    ctrlCnlProps.ObjNum = (int)rowView["ObjNum"];
                    tblObj.DefaultView.RowFilter = "ObjNum = " + ctrlCnlProps.ObjNum;
                    ctrlCnlProps.ObjName = tblObj.DefaultView.Count > 0 ? (string)tblObj.DefaultView[0]["Name"] : "";

                    // ����������� ������ � ������������ ��
                    ctrlCnlProps.KPNum = (int)rowView["KPNum"];
                    tblKP.DefaultView.RowFilter = "KPNum = " + ctrlCnlProps.KPNum;
                    ctrlCnlProps.KPName = tblKP.DefaultView.Count > 0 ? (string)tblKP.DefaultView[0]["Name"] : "";

                    // ����������� �������� �������
                    tblCmdVal.DefaultView.RowFilter = "CmdValID = " + rowView["CmdValID"];
                    if (tblCmdVal.DefaultView.Count > 0)
                    {
                        string val = (string)tblCmdVal.DefaultView[0]["Val"];
                        ctrlCnlProps.CmdValArr = val.Split(';'); // ������� ������ ��������
                        for (int i = 0; i < ctrlCnlProps.CmdValArr.Length; i++)
                            ctrlCnlProps.CmdValArr[i] = ctrlCnlProps.CmdValArr[i].Trim();
                        if (ctrlCnlProps.CmdValArr.Length == 1 && ctrlCnlProps.CmdValArr[0] == "")
                            ctrlCnlProps.CmdValArr = null;
                    }
                    else
                    {
                        ctrlCnlProps.CmdValArr = null;
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction(string.Format(Localization.UseRussian ?
                    "������ ��� ��������� ������� ������ ���������� {0}: {1}" :
                    "Error getting output channel {0} properties: {1}", ctrlCnlNum, ex.Message), 
                    Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(baseLock);
            }

            return ctrlCnlProps;
        }


		/// <summary>
		/// �������� ������ ������ �������� �����
		/// </summary>
		public void GetCurData(int cnlNum, out double val, out int stat)
		{
            Monitor.Enter(cnlDataLock);
			val = 0.0;
			stat = 0;

            try
            {
                if (tblCur.SrezList.Count > 0)
                {
                    SrezTableLight.Srez srez = tblCur.SrezList.Values[0];
                    SrezTableLight.CnlData cnlData;
                    bool found = srez.GetCnlData(cnlNum, out cnlData);
                    if (found)
                    {
                        val = cnlData.Val;
                        stat = cnlData.Stat;
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction(string.Format(Localization.UseRussian ?
                    "������ ��� ��������� ������ ������ {0} �������� �����: {1}" :
                    "Error getting channel {0} current data: {1}", cnlNum, ex.Message), Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(cnlDataLock);
            }
        }

		/// <summary>
		/// �������� ������ ������ �������� ����� �� ��������� ������� ������
		/// </summary>
        public void GetHourData(SrezTableLight hourTable, int cnlNum, DateTime dateTime, 
            out double val, out int stat)
		{
            Monitor.Enter(cnlDataLock);
            val = 0.0;
			stat = 0;

            try
            {
                SrezTableLight.Srez srez;
                if (hourTable != null && hourTable.SrezList.TryGetValue(dateTime, out srez))
                {
                    SrezTableLight.CnlData cnlData;
                    bool found = srez.GetCnlData(cnlNum, out cnlData);
                    if (found)
                    {
                        val = cnlData.Val;
                        stat = cnlData.Stat;
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction(string.Format(Localization.UseRussian ?
                    "������ ��� ��������� ������ ������ {0} �������� �����: {1}" :
                    "Error getting channel {0} hour data: {1}", cnlNum, ex.Message), Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(cnlDataLock);
            }
        }
        
        /// <summary>
        /// �������� �������, ��������������� �������, �� ������� �������
        /// </summary>
        /// <remarks>���� cnlsFilter ����� null, �� ���������� �� ������������</remarks>
        public List<EventTableLight.Event> GetEvents(EventTableLight eventTable, List<int> cnlsFilter)
        {
            Monitor.Enter(eventLock);
            List<EventTableLight.Event> eventList = null;

            try
            {
                if (eventTable != null)
                {
                    if (cnlsFilter == null)
                    {
                        eventList = eventTable.AllEvents;
                    }
                    else
                    {
                        eventTable.Filters = EventTableLight.EventFilters.Cnls;
                        eventTable.CnlsFilter = cnlsFilter;
                        eventList = eventTable.FilteredEvents;
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ��������� �������: " : 
                    "Error getting events: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(eventLock);
            }

            return eventList;
        }

        /// <summary>
        /// �������� �������, ��������������� �������, �� ������� �������, ������� � ��������� ������ �������
        /// </summary>
        /// <remarks>���� cnlsFilter ����� null, �� ���������� �� ������������</remarks>
        public List<EventTableLight.Event> GetEvents(EventTableLight eventTable, List<int> cnlsFilter, 
            int startEvNum)
        {
            Monitor.Enter(eventLock);
            List<EventTableLight.Event> eventList = null;

            try
            {
                if (eventTable != null)
                {
                    eventTable.Filters = cnlsFilter == null ?
                        EventTableLight.EventFilters.None : EventTableLight.EventFilters.Cnls;
                    eventTable.CnlsFilter = cnlsFilter;
                    eventList = eventTable.GetEvents(startEvNum);
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? 
                    "������ ��� ��������� �������, ������� � ��������� ������: " :
                    "Error getting events starting from the specified number: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(eventLock);
            }

            return eventList;
        }

        /// <summary>
        /// �������� �������� ���������� ��������� �������, ��������������� �������
        /// </summary>
        /// <remarks>���� cnlsFilter ����� null, �� ���������� �� ������������</remarks>
        public List<EventTableLight.Event> GetLastEvents(EventTableLight eventTable, List<int> cnlsFilter, 
            int count)
        {
            Monitor.Enter(eventLock);
            List<EventTableLight.Event> eventList = null;

            try
            {
                if (eventTable != null)
                {
                    eventTable.Filters = cnlsFilter == null ?
                        EventTableLight.EventFilters.None : EventTableLight.EventFilters.Cnls;
                    eventTable.CnlsFilter = cnlsFilter;
                    eventList = eventTable.GetLastEvents(count);
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ��������� ��������� �������: " :
                    "Error getting last events: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(eventLock);
            }

            return eventList;
        }

        /// <summary>
        /// �������� ������� �� ������� ������� �� ������
        /// </summary>
        public EventTableLight.Event GetEventByNum(EventTableLight eventTable, int evNum)
        {
            Monitor.Enter(eventLock);
            EventTableLight.Event ev = null;

            try
            {
                if (1 <= evNum && evNum <= eventTable.AllEvents.Count &&
                    eventTable.AllEvents[evNum - 1].Number == evNum)
                    ev = eventTable.AllEvents[evNum - 1];
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ��������� ������� �� ������: " :
                    "Error getting event by number: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(eventLock);
            }

            return ev;
        }

        /// <summary>
        /// ������������� ������� � ������� ��� ����������� �����
        /// </summary>
        public EventView ConvertEvent(EventTableLight.Event ev)
        {
            Monitor.Enter(baseLock);
            EventView eventView = null;

            try 
            {
                if (ev != null)
                    eventView = ConvEvent(ev);
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? 
                    "������ ��� �������������� ������� � ������� ��� ����������� �����: " :
                    "Error converting event to a suitable view") + ex.Message, Log.ActTypes.Exception);
            }
            finally 
            {
                Monitor.Exit(baseLock); 
            }

            return eventView;
        }

        /// <summary>
        /// ������������� ������ ������� � ������� ��� ����������� �����
        /// </summary>
        public List<EventView> ConvertEvents(List<EventTableLight.Event> eventList)
        {
            Monitor.Enter(baseLock);
            List<EventView> eventViewList = new List<EventView>();

            try
            {
                if (eventList != null)
                    foreach (EventTableLight.Event ev in eventList)
                        eventViewList.Add(ConvEvent(ev));                
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ?
                    "������ ��� �������������� ������ ������� � ������� ��� ����������� �����: " :
                    "Error converting events list to a suitable view") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(baseLock);
            }

            return eventViewList;
        }

		/// <summary>
		/// �������� ������ ������ ��������� ����� �� �����
		/// </summary>
		public Trend GetMinData(int cnlNum, DateTime date)
		{
            Monitor.Enter(cnlDataLock);
            try
            {
                string minTableName = "m" + date.ToString("yyMMdd") + ".dat";
                if (trend == null || trend.CnlNum != cnlNum || trend.TableName != minTableName)
                {
                    trend = new Trend(cnlNum);
                    trend.FileModTime = serverComm.ReceiveTrend(minTableName, date, trend) ?
                        serverComm.ReceiveFileAge(ServerComm.Dirs.Min, minTableName) : DateTime.MinValue;
                }
                else
                {
                    DateTime minModTime = serverComm.ReceiveFileAge(ServerComm.Dirs.Min, minTableName);
                    if ((DateTime.Now - minModTime).TotalSeconds > MinSrezValidTime /*������ ��������*/ &&
                        minModTime != trend.FileModTime /*���� ������ ������*/)
                    {
                        trend = new Trend(cnlNum);
                        trend.FileModTime = serverComm.ReceiveTrend(minTableName, date, trend) ?
                            minModTime : DateTime.MinValue;
                    }
                }
                return trend;
            }
            finally
            {
                Monitor.Exit(cnlDataLock);
            }
        }


		/// <summary>
		/// �������� ����������������� ������� �������� ������
		/// </summary>
		public string GetCnlVal(int cnlNum, bool showUnit, out string color)
		{
            return GetCnlVal(null, cnlNum, DateTime.MinValue, showUnit, out color);
		}

		/// <summary>
        /// �������� ����������������� �������� ������ �� �������� ��� �������� ����� �� ��������� �����
		/// </summary>
        /// <remarks>��� �������� �������� hourTable ����� null ��� dataDT ����� DateTime.MinValue</remarks>
        public string GetCnlVal(SrezTableLight hourTable, int cnlNum, DateTime dateTime, bool showUnit, 
            out string color)
		{
            // ��������� �������� � ������� ������
            double val;
            int stat;

            if (dateTime == DateTime.MinValue || hourTable == null)
                GetCurData(cnlNum, out val, out stat);
            else
                GetHourData(hourTable, cnlNum, dateTime, out val, out stat);

            // �������������� �������� ������
            bool isNumber;
            return FormatCnlVal(val, stat, GetCnlProps(cnlNum), showUnit, true, dateTime, DateTime.Now, 
                out isNumber, out color);
		}

        /// <summary>
        /// ������������� �������� �������� ������
        /// </summary>
        /// <remarks>��� �������� �������� dataDT ����� DateTime.MinValue</remarks>
        public string FormatCnlVal(double val, int stat, CnlProps cnlProps, bool showUnit, bool getColor, 
            DateTime dataDT, DateTime nowDT, out bool isNumber, out string color,
            string decSep = null, string grSep = null)
        {
            string result = "";
            isNumber = false;
            color = "black";

            try
            {
                // ����������� ����� ������� ������������ ������
                int unitArrLen = cnlProps == null || cnlProps.UnitArr == null ? 0 : cnlProps.UnitArr.Length;

                // ����������� �����
                if (cnlProps != null && getColor)
                {
                    if (!cnlProps.ShowNumber && unitArrLen == 2 && stat > 0 && 
                        stat != BaseValues.ParamStat.FormulaError && stat != BaseValues.ParamStat.Unreliable)
                    {
                        color = val > 0 ? "green" : "red";
                    }
                    else
                    {
                        Monitor.Enter(baseLock);
                        try
                        {
                            string colorByStat;
                            if (GetColorByStat(stat, out colorByStat))
                                color = colorByStat;
                        }
                        finally
                        {
                            Monitor.Exit(baseLock);
                        }
                    }
                }

                // ����������� ���������� ������
                if (cnlProps == null || cnlProps.ShowNumber)
                {
                    string unit = showUnit && unitArrLen > 0 ? " " + cnlProps.UnitArr[0] : "";
                    isNumber = unit == "";

                    nfi.NumberDecimalDigits = cnlProps == null ? 3 : cnlProps.DecDigits;
                    nfi.NumberDecimalSeparator = decSep == null ? defDecSep : decSep;
                    nfi.NumberGroupSeparator = grSep == null ? defGrSep : grSep;
                    result = val.ToString("N", nfi) + unit;
                }
                else if (unitArrLen > 0)
                {
                    int unitInd = (int)val;
                    if (unitInd < 0)
                        unitInd = 0;
                    else if (unitInd >= unitArrLen)
                        unitInd = unitArrLen - 1;
                    result = cnlProps.UnitArr[unitInd];
                }

                // ��������� ���������� ������, ���� �������� ������ �� ����������			
                if (dataDT == DateTime.MinValue)
                {
                    if ((nowDT - tblCur.FileModTime).TotalMinutes > CurSrezShowTime) // ������� ���� �������
                    {
                        result = "";
                        isNumber = false;
                    }
                    else if (stat == 0)
                    {
                        result = "---";
                        isNumber = false;
                    }
                }
                else if (stat == 0)
                {
                    result = "---";
                    isNumber = false;

                    if (dataDT.Date > nowDT.Date)
                    {
                        result = "";
                    }
                    else if (dataDT.Date == nowDT.Date)
                    {
                        if (dataDT.Hour > nowDT.Hour + 1)
                            result = "";
                        else if (dataDT.Hour == nowDT.Hour + 1)
                        {
                            result = "***";
                            color = "green";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string cnlNumStr = cnlProps == null ? "" : " " + cnlProps.CnlNum;
                AppData.Log.WriteAction(string.Format(Localization.UseRussian ? 
                    "������ ��� �������������� �������� �������� ������{0}: {1}" : 
                    "Error formatting input channel{0} value: {1}", cnlNumStr, ex.Message), Log.ActTypes.Exception);
            }

            return result;
        }


		/// <summary>
        /// ��������� ������������ ����� � ������ ������������, �������� ��� ����
		/// </summary>
        public bool CheckUser(string login, string password, bool checkPassword, out int roleID, out string errMsg)
		{
            // ���������� ������� ��� ������ ������� �� SCADA-�������� ��� �������������
            RefrServerComm();

            if (serverComm == null)
            {
                roleID = (int)ServerComm.Roles.Disabled;
                errMsg = WebPhrases.CommSettingsNotLoaded;
                return false;
            }
            else
            {
                if (checkPassword && string.IsNullOrEmpty(password))
                {
                    roleID = (int)ServerComm.Roles.Err;
                    errMsg = WebPhrases.WrongPassword;
                    return false;
                }
                else
                {
                    // �������� ������������
                    if (serverComm.CheckUser(login, checkPassword ? password : null, out roleID))
                    {
                        if (roleID == (int)ServerComm.Roles.Disabled)
                            errMsg = WebPhrases.NoRightsL;
                        else if (roleID == (int)ServerComm.Roles.App)
                            errMsg = WebPhrases.IllegalRole;
                        else if (roleID == (int)ServerComm.Roles.Err)
                            errMsg = WebPhrases.WrongPassword;
                        else
                            errMsg = "";

                        return errMsg == "";
                    }
                    else
                    {
                        errMsg = WebPhrases.ServerUnavailable;
                        return false;
                    }
                }
            }
		}

        /// <summary>
        /// �������� ������������� ������������ �� �����
        /// </summary>
        public int GetUserID(string login)
        {
            // ���������� ������ ���� ������������ ��� �������������
            RefreshBase();

            Monitor.Enter(baseLock);
            int userID = 0;

            try
            {
                tblUser.DefaultView.RowFilter = "Name = '" + login + "'";
                if (tblUser.DefaultView.Count > 0)
                    userID = (int)tblUser.DefaultView[0]["UserID"];
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? 
                    "������ ��� ��������� �������������� ������������ �� �����: " : 
                    "Error getting user ID by name: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(baseLock);
            }

            return userID;
        }

        /// <summary>
        /// �������� ������ ���� �� ������� ���������� ��� ��������� �������������� ����
        /// </summary>
        public SortedList<string, Right> GetRightList(int roleID)
        {
            // ���������� ������ ���� ������������ ��� �������������
            RefreshBase();

            Monitor.Enter(baseLock);
            SortedList<string, Right> rightList = new SortedList<string, Right>();

            try
            {
                tblRight.DefaultView.RowFilter = "RoleID = " + roleID;
                int rowCnt = tblRight.DefaultView.Count;

                for (int i = 0; i < rowCnt; i++)
                {
                    DataRowView rowView = tblRight.DefaultView[i];
                    tblInterface.DefaultView.RowFilter = "ItfID = " + rowView["ItfID"];

                    if (tblInterface.DefaultView.Count > 0)
                    {
                        Right right = new Right();
                        right.ViewRight = (bool)rowView["ViewRight"];
                        right.CtrlRight = (bool)rowView["CtrlRight"];

                        string name = (string)tblInterface.DefaultView[0]["Name"];
                        if (!rightList.ContainsKey(name))
                            rightList.Add(name, right);
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? 
                    "������ ��� ��������� ������ ���� �� ������� ����������: " : 
                    "Error getting interface objects rights list: ") + ex.Message, Log.ActTypes.Exception);
            }
            finally
            {
                Monitor.Exit(baseLock);
            }

            return rightList;
        }

        /// <summary>
        /// �������� ������������ ���� �� ��������������
        /// </summary>
        public string GetRoleName(int roleID)
        {
            string roleName = ServerComm.GetRoleName(roleID);

            if ((int)ServerComm.Roles.Custom <= roleID && roleID < (int)ServerComm.Roles.Err)
            {
                // ���������� ������ ���� ������������ ��� �������������
                RefreshBase();

                // ��������� ������������ ���������������� ���� �� ���� ������������
                Monitor.Enter(baseLock);
                try
                {
                    tblRole.DefaultView.RowFilter = "RoleID = " + roleID;
                    if (tblRole.DefaultView.Count > 0)
                        roleName = (string)tblRole.DefaultView[0]["Name"];
                }
                catch (Exception ex)
                {
                    AppData.Log.WriteAction((Localization.UseRussian ? 
                        "������ ��� ��������� ������������ ���� �� ��������������: " :
                        "Error getting role name by ID: ") +  ex.Message, Log.ActTypes.Exception);
                }
                finally
                {
                    Monitor.Exit(baseLock);
                }
            }

            return roleName;
        }
	}
}