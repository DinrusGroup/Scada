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
 * Summary  : Application user data
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2007
 * Modified : 2014
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.SessionState;
using Scada.Client;
using Utils;

namespace Scada.Web
{
    /// <summary>
    /// Application user data
    /// <para>������ ������������ ����������</para>
    /// </summary>
    public class UserData
    {
        /// <summary>
        /// ����� �� ����� ������������� � �������� � ���� �������������
        /// </summary>
        private class ViewSetRight
        {
            /// <summary>
            /// �����������
            /// </summary>
            public ViewSetRight(ViewSettings.ViewSet viewSet)
            {
                ViewSet = viewSet;
                Right = MainData.Right.NoRights;
                ViewRightArr = null;
            }

            /// <summary>
            /// �������� ����� �������������
            /// </summary>
            public ViewSettings.ViewSet ViewSet { get; private set; }
            /// <summary>
            /// �������� ��� ���������� ����� �� ����� �������������
            /// </summary>
            public MainData.Right Right { get; set; }
            /// <summary>
            /// �������� ��� ���������� ������ ���� �� ������������� �� ������
            /// </summary>
            public MainData.Right[] ViewRightArr { get; set; }
        }


        private SortedList<string, MainData.Right> rightList; // ������ ���� ������������ �� ������� ����������
        private List<ViewSetRight> viewSetRightList; // ������ ���� �� ������ ������������� � �������������


        /// <summary>
        /// �����������
        /// </summary>
        private UserData()
        {
            ViewSettings = new ViewSettings();
            Logout();
        }


        /// <summary>
        /// �������� ��� ������������
        /// </summary>
        public string UserLogin { get; private set; }

        /// <summary>
        /// �������� ������������� ������������ � ���� ������������
        /// </summary>
        public int UserID { get; private set; }

        /// <summary>
        /// �������� ���� ������������
        /// </summary>
        public ServerComm.Roles Role { get; private set; }

        /// <summary>
        /// �������� ������������� ���� ������������
        /// </summary>
        public int RoleID { get; private set; }

        /// <summary>
        /// �������� ������������ ���� ������������
        /// </summary>
        public string RoleName { get; private set; }

        /// <summary>
        /// �������� �������, �������� �� ���� ������������ � �������
        /// </summary>
        public bool LoggedOn { get; private set; }

        /// <summary>
        /// �������� ���� � ����� ����� ������������ � �������
        /// </summary>
        public DateTime LogOnDT { get; private set; }
        
        /// <summary>
        /// �������� ��������� �������������
        /// </summary>
        public ViewSettings ViewSettings { get; private set; }


        /// <summary>
        /// ���������������� ������ ���� �� ������ ������������� � �������� � ��� �������������
        /// </summary>
        private void InitViewSetRightList(List<ViewSettings.ViewSet> viewSetList)
        {
            viewSetRightList = new List<ViewSetRight>();

            if (viewSetList != null)
            {
                foreach (ViewSettings.ViewSet viewSet in viewSetList)
                {
                    ViewSetRight viewSetRight = new ViewSetRight(viewSet);
                    viewSetRight.Right = GetRight(viewSet.Name);
                    viewSetRightList.Add(viewSetRight);
                }
            }
        }
        
        /// <summary>
        /// ���������������� ����� �� ������������� �� ������
        /// </summary>
        private void InitViewRightArr(ViewSetRight viewSetRight)
        {
            ViewSettings.ViewSet viewSet = viewSetRight.ViewSet;

            if (viewSet != null && viewSet.Count > 0)
            {
                bool viewSetViewRight = viewSetRight.Right.ViewRight;
                bool viewSetCtrlRight = viewSetRight.Right.CtrlRight;
                int viewCnt = viewSet.Count;
                MainData.Right[] viewRightArr = new MainData.Right[viewCnt];

                for (int i = 0; i < viewCnt; i++)
                {
                    MainData.Right right = GetRight(Path.GetFileName(viewSet[i].FileName));
                    viewRightArr[i].ViewRight = right.ViewRight && viewSetViewRight;
                    viewRightArr[i].CtrlRight = right.CtrlRight && viewSetCtrlRight;
                }

                viewSetRight.ViewRightArr = viewRightArr;
            }
        }
        

        /// <summary>
        /// ��������� ���� ������������ � �������
        /// </summary>
        /// <remarks>���� ������ ����� null, �� �� �� �����������</remarks>
        public bool Login(string login, string password, out string errMsg)
        {
            login = login == null ? "" : login.Trim();
            int roleID;

            if (AppData.MainData.CheckUser(login, password, password != null, out roleID, out errMsg))
            {
                UserLogin = login;
                Role = ServerComm.GetRole(roleID);
                RoleID = roleID;
                RoleName = AppData.MainData.GetRoleName(RoleID);
                UserID = AppData.MainData.GetUserID(login);

                LoggedOn = true;
                LogOnDT = DateTime.Now;
                rightList = AppData.MainData.GetRightList(roleID);
                InitViewSetRightList(ViewSettings.ViewSetList);

                AppData.Log.WriteAction((password == null ? 
                    (Localization.UseRussian ? "���� � ������� ��� ������: " : "Login without a password: ") : 
                    (Localization.UseRussian ? "���� � �������: " : "Login: ")) +
                    login + " (" + RoleName + ")", Log.ActTypes.Action);
                return true;
            }
            else
            {
                Logout();

                string err = login == "" ? errMsg : login + " - " + errMsg;
                AppData.Log.WriteAction((Localization.UseRussian ? "��������� ������� ����� � �������: " : 
                    "Unsuccessful login attempt: ") + err, Log.ActTypes.Error);
                return false;
            }
        }

        /// <summary>
        /// ��������� ���� ������������ � ������� ��� �������� ������
        /// </summary>
        public bool Login(string login)
        {
            string errMsg;
            return Login(login, null, out errMsg);
        }

        /// <summary>
        /// ��������� ������ ������������ � ������������ ����� ������
        /// </summary>
        public void Logout()
        {
            UserLogin = "";
            UserID = 0;
            Role = ServerComm.Roles.Disabled;
            RoleID = (int)Role;
            RoleName = "";
            LoggedOn = false;
            LogOnDT = DateTime.MinValue;
            ViewSettings.ClearViewCash();

            rightList = null;
            viewSetRightList = null;
        }

        /// <summary>
        /// �������� ����� ������������ �� ������ ����������
        /// </summary>
        public MainData.Right GetRight(string itfObjName)
        {
            MainData.Right right;

            if (Role == ServerComm.Roles.Custom)
            {
                if (rightList == null || !rightList.TryGetValue(itfObjName, out right))
                    right = MainData.Right.NoRights;
            }
            else
            {
                right = new MainData.Right();
                right.CtrlRight = Role == ServerComm.Roles.Admin || Role == ServerComm.Roles.Dispatcher;
                right.ViewRight = right.CtrlRight || Role == ServerComm.Roles.Guest;
            }

            return right;
        }

        /// <summary>
        /// �������� ����� ������������ �� ����� �������������
        /// </summary>
        public MainData.Right GetViewSetRight(int viewSetIndex)
        {
            return viewSetRightList != null && 0 <= viewSetIndex && viewSetIndex < viewSetRightList.Count ?
                viewSetRightList[viewSetIndex].Right : MainData.Right.NoRights;
        }

        /// <summary>
        /// �������� ����� ������������ �� �������������
        /// </summary>
        public MainData.Right GetViewRight(int viewSetIndex, int viewIndex)
        {
            MainData.Right right = MainData.Right.NoRights;

            if (viewSetRightList != null && 0 <= viewSetIndex && viewSetIndex < viewSetRightList.Count)
            {
                ViewSetRight viewSetRight = viewSetRightList[viewSetIndex];

                if (viewSetRight.ViewRightArr == null)
                    InitViewRightArr(viewSetRight);

                MainData.Right[] viewRightArr = viewSetRight.ViewRightArr;

                if (viewRightArr != null && 0 <= viewIndex && viewIndex < viewRightArr.Length)
                    right = viewRightArr[viewIndex];
            }

            return right;
        }

        /// <summary>
        /// �������� ������������� ��������� ���� � ����� �� ����
        /// </summary>
        public bool GetView(Type viewType, int viewSetIndex, int viewIndex, 
            out BaseView view, out MainData.Right right)
        {
            bool result = false;
            view = null;
            right = MainData.Right.NoRights;

            try
            {
                if (viewSetRightList != null && 0 <= viewSetIndex && viewSetIndex < viewSetRightList.Count)
                {
                    ViewSetRight viewSetRight = viewSetRightList[viewSetIndex];
                    ViewSettings.ViewSet viewSet = viewSetRight.ViewSet;

                    if (viewSetRight.ViewRightArr == null)
                        InitViewRightArr(viewSetRight);

                    MainData.Right[] viewRightArr = viewSetRight.ViewRightArr;

                    if (viewSet != null && viewRightArr != null && 0 <= viewIndex && 
                        viewIndex < viewSet.Count && viewIndex < viewRightArr.Length)
                    {
                        ViewSettings.ViewInfo viewInfo = viewSet[viewIndex];
                        right = viewRightArr[viewIndex];

                        if (viewType == null)
                        {
                            view = viewInfo.ViewCash;
                            return view != null;
                        }
                        else if (viewInfo.Type == viewType.Name)
                        {
                            if (viewInfo.ViewCash != null && viewInfo.ViewCash.GetType() == viewType)
                            {
                                view = viewInfo.ViewCash;
                                result = true;
                            }
                            else
                            {
                                view = (BaseView)Activator.CreateInstance(viewType);

                                if (!view.StoredOnServer)
                                    view.ItfObjName = Path.GetFileName(viewInfo.FileName);

                                if (!view.StoredOnServer || 
                                    AppData.MainData.ServerComm.ReceiveView(viewSet.Directory + viewInfo.FileName, view))
                                {
                                    AppData.MainData.RefreshBase();
                                    view.BindCnlProps(AppData.MainData.CnlPropsArr);
                                    viewInfo.ViewCash = view;
                                    result = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppData.Log.WriteAction((Localization.UseRussian ? "������ ��� ��������� �������������: " : 
                    "Error getting view: ") + ex.Message, Log.ActTypes.Exception);
            }

            return result;
        }


        /// <summary>
        /// �������� ������ ������������ ����������
        /// </summary>
        /// <remarks>��� ���-���������� ������ ������������ ����������� � ������</remarks>
        public static UserData GetUserData()
        {
            HttpSessionState session = HttpContext.Current == null ? null : HttpContext.Current.Session;
            UserData userData = session == null ? null : session["UserData"] as UserData;

            if (userData == null)
            {
                AppData.InitAppData();
                userData = new UserData();

                if (session != null)
                    session.Add("UserData", userData);

                // �������� �������� �������������
                string errMsg;
                if (!userData.ViewSettings.LoadFromFile(AppData.ConfigDir + ViewSettings.DefFileName, out errMsg))
                    AppData.Log.WriteAction(errMsg, Log.ActTypes.Exception);
            }

            return userData;
        }
    }
}