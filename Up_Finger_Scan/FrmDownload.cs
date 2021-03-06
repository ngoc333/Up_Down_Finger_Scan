﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.OracleClient;
using System.Threading.Tasks;
using System.Threading;
using System.Data.OleDb;
using System.Data.SqlClient;

//using Oracle.DataAccess.Client;


namespace ATTN
{
    public partial class FrmDownload : Form
    {
        public FrmDownload()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        UpService _db = new UpService();
        Dictionary<string, string> _dtnTypeDb = new Dictionary<string, string>();
        Thread _thrd;
        string _strData = "VJ";
        #region Event

        private void FrmUpload_Load(object sender, EventArgs e)
        {
            DataSet ds = Lib.ClassLib.ReadXML(Application.StartupPath + "\\configATTN.XML");
            setCbo(cboLocal, ds.Tables["DB_LOCAL"]);
            setCbo(cboSever, ds.Tables["DB_SERVER"]);
            _strData = ds.Tables["config"].Rows[0]["DATA"].ToString();
            
        }

        private void CmdStart_Click(object sender, EventArgs e)
        {
            cmdStart.Enabled = false;
            RunDownload();
        }

        [Obsolete]
        private void CmdCheckDb_Click(object sender, EventArgs e)
        {
            bool bLocal = checkDB(cboLocal);
            bool bSever = checkDB(cboSever);
            if (bLocal && bSever)
                writeLog("Connect Success");
            else
                writeLog("Local: " + bLocal.ToString() + ";  Sever: " + bSever.ToString());
        }

        private void FrmUpload_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
        #endregion

        #region UNIS
        [Obsolete]
        private void downloadUNIS()
        {
            try
            {
                writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Start Download" );

                string strSever = cboSever.SelectedValue.ToString();
                string strLocal = cboLocal.SelectedValue.ToString();
                string strUser = System.Net.Dns.GetHostName();
                string strIP = System.Net.Dns.GetHostEntry(strUser).AddressList.GetValue(1).ToString();

                

                using (OleDbConnection con = new OleDbConnection(strLocal))
                {
                    con.Open();
                    DataTable dtLocal = new DataTable();
                    string strSql = "";   
                    
                    /*****************
                     * Local
                     *
                     *****************/                   
                    using (OleDbCommand cmd = new OleDbCommand())
                    {
                        cmd.Connection = con;
                        //Update status tUser
                        cmd.CommandText = "  UPDATE TUSER SET VALID_YN = 'N'";
                        writeLog("  UPDATE tUser SET VALID_YN = 'N': " + cmd.ExecuteNonQuery().ToString());
                        //Delete data table cPost
                        cmd.CommandText = "DELETE FROM CPOST";
                        writeLog("  DELETE cPost: " + cmd.ExecuteNonQuery().ToString());
                        //Delete data table tEmployee
                        cmd.CommandText = "DELETE FROM TEMPLOYE";
                        writeLog("  DELETE tEmployee: " + cmd.ExecuteNonQuery().ToString());
                        //Delete data table iUserCard
                        cmd.CommandText = "DELETE FROM iUserCard";
                        writeLog("  DELETE iUserCard: " + cmd.ExecuteNonQuery().ToString());
                    }
                    /*****************
                     * Sever
                     *
                     *****************/
                    DataTable dtSever = new DataTable();
                    DataTable dtDept = new DataTable();
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin get data");
                    using (OracleConnection connection = new OracleConnection(strSever))
                    {
                        connection.Open();
                        using (OracleCommand command = new OracleCommand("PW_TL_SCAN_MC_DOWNLOAD_S", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add("ARG_DATA", OracleType.VarChar).Value = _strData;
                            command.Parameters.Add("ARG_NAME_DB", OracleType.VarChar).Value = cboLocal.Text;
                            command.Parameters.Add("ARG_IP", OracleType.VarChar).Value = strIP;
                            command.Parameters.Add("ARG_USER", OracleType.VarChar).Value = strUser;
                            command.Parameters.Add("OUT_CURSOR", OracleType.Cursor);
                            command.Parameters.Add("OUT_CURSOR2", OracleType.Cursor);

                            command.Parameters["ARG_DATA"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_NAME_DB"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_IP"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_USER"].Direction = ParameterDirection.Input;
                            command.Parameters["OUT_CURSOR"].Direction = ParameterDirection.Output;
                            command.Parameters["OUT_CURSOR2"].Direction = ParameterDirection.Output;

                            DataSet ds = new DataSet();
                            (new OracleDataAdapter(command)).Fill(ds);
                            dtSever = ds.Tables[0];
                            dtDept = ds.Tables[1];
                        }
                    }

                    if (dtSever == null || dtSever.Rows.Count == 0)
                    {
                        writeLog("No Data!" );
                        return;
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End get data");

                    writeLog("  Insert cPOST--> " + dtDept.Rows.Count);
                    foreach (DataRow row in dtDept.Rows)
                    {
                        strSql = string.Format("INSERT INTO cPOST(C_CODE, C_Name) " +
                                                 "        VALUES ('{0}', '{1}') ",
                                                        row["DEP_CODE"], row["DEP_NAME"]);
                        execAccess(strSql, con, "  Insert cPOST-->" + row["DEP_CODE"] + "|" + row["DEP_NAME"]);
                    }

                    int iRowUpload = 0, iRow = dtSever.Rows.Count;

                    
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Begin tUser: " + iRow.ToString());

                    //Get empid in table tUser
                    using (OleDbDataAdapter da = new OleDbDataAdapter("SELECT L_ID, C_Unique FROM TUSER ", con))
                    {
                        da.Fill(dtLocal);
                    }

                    foreach (DataRow row in dtSever.Rows)
                    {
                        if (dtLocal.Select("L_ID = '" + row["EMP_NO"] + "'").Count() == 0)
                        {
                            //Insert table tUser
                            strSql = string.Format("INSERT INTO TUSER( L_ID,             C_Name,        C_Unique,              " +
                                                                     " L_Type,           C_RegDate,     L_OptDateLimit,  " +
                                                                     " C_DateLimit,      L_AccessType,  C_Password,    " +
                                                                     " L_Identify,       L_VerifyLevel, C_AccessGroup, " +
                                                                     " C_PassbackStatus, L_IsNotice,    Valid_YN) " +
                                                             " VALUES( '{0}', '{1}', '{3}', " +
                                                                     "  0 ,   '{6}',  0, " +
                                                                     " '{7}',  0,    '{2}', " +
                                                                     "  1,     0,    '{5}', " +
                                                                     " '****', 0,    'Y' )",
                                                row["EMP_NO"], row["ENG_NAME"], row["EMPID"],
                                                row["DEP_CODE"], row["RF_ID"], row["DEPT_GROUP"],
                                                row["REG_DATE"], row["DATE_LIMIT"]);
                            execAccess(strSql, con, "  Insert tUser-->" + row["EMP_NO"]);

                           
                        }
                        else
                        {
                            strSql = string.Format(  "UPDATE TUSER " +
                                                     "   SET VALID_YN = 'Y'   " +
                                                     "     , C_AccessGroup = '{5}'" +
                                                     " WHERE L_ID = {0}",
                                                     row["EMP_NO"], row["ENG_NAME"], row["EMPID"],
                                                     row["DEP_CODE"], row["RF_ID"], row["DEPT_GROUP"] );
                            execAccess(strSql, con, "  Update tUser-->" + row["EMP_NO"]);
                        }

                        //Insert table tEmployee
                        strSql = string.Format("INSERT INTO TEMPLOYE(L_UID, C_POST) " +
                                                             "VALUES ( {0}, '{3}' )",
                                                row["EMP_NO"], row["ENG_NAME"], row["EMPID"],
                                                row["DEP_CODE"], row["RF_ID"], row["DEPT_GROUP"]);
                        execAccess(strSql, con, "  Insert tEmployee-->" + row["EMP_NO"]);

                        //Insert table iUserCard
                        if (row["RF_ID"].ToString().Trim() != "")
                        {
                            strSql = string.Format(" INSERT INTO iUserCard(C_CardNum, L_UID) " +
                                                        " VALUES ( '{4}', {0} ) ",
                                                    row["EMP_NO"], row["ENG_NAME"], row["EMPID"],
                                                    row["DEP_CODE"], row["RF_ID"], row["DEPT_GROUP"]);
                            execAccess(strSql, con, "  Insert iUserCard-->" + row["EMP_NO"]);
                        }

                        lblStatus.Text = "Upload: " + (++iRowUpload).ToString() + "/" + iRow.ToString();
                    }



                    lblStatus.Text = "";
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->End tUser");
                }

            }
            catch (Exception ex)
            {
                writeLog("  Error !!!");
                Lib.ClassLib.writeToLog("uploadUNIS : " + ex.ToString());
            }
            finally
            {
                cmdStart.Enabled = true;
                writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Finish Download ");
            }
        }
        #endregion

        #region  Access
        [Obsolete]
        private void downloadACCESS()
        {
            try
            {
                writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Start Download");

                string strSever = cboSever.SelectedValue.ToString();
                string strLocal = cboLocal.SelectedValue.ToString();
                string strUser = System.Net.Dns.GetHostName();
                string strIP = System.Net.Dns.GetHostEntry(strUser).AddressList.GetValue(1).ToString();



                using (OleDbConnection con = new OleDbConnection(strLocal))
                {
                    con.Open();
                    DataTable dtLocal = new DataTable();
                    string strSql = "";

                    /*****************
                     * Local
                     *
                     *****************/
                    //Update status tUser
                    using (OleDbCommand cmd = new OleDbCommand("  UPDATE TUSER SET VALID_YN = 'N'", con))
                    {
                        writeLog("  UPDATE tUser SET VALID_YN = 'N': " + cmd.ExecuteNonQuery().ToString());
                    }
                    /*****************
                     * Sever
                     *
                     *****************/
                    DataTable dtSever = new DataTable();
                    DataTable dtDept = new DataTable();
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin get data");
                    using (OracleConnection connection = new OracleConnection(strSever))
                    {
                        connection.Open();
                        using (OracleCommand command = new OracleCommand("PW_TL_SCAN_MC_DOWNLOAD_S", connection))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.Add("ARG_DATA", OracleType.VarChar).Value = _strData;
                            command.Parameters.Add("ARG_NAME_DB", OracleType.VarChar).Value = cboLocal.Text;
                            command.Parameters.Add("ARG_IP", OracleType.VarChar).Value = strIP;
                            command.Parameters.Add("ARG_USER", OracleType.VarChar).Value = strUser;
                            command.Parameters.Add("OUT_CURSOR", OracleType.Cursor);
                            command.Parameters.Add("OUT_CURSOR2", OracleType.Cursor);

                            command.Parameters["ARG_DATA"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_NAME_DB"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_IP"].Direction = ParameterDirection.Input;
                            command.Parameters["ARG_USER"].Direction = ParameterDirection.Input;
                            command.Parameters["OUT_CURSOR"].Direction = ParameterDirection.Output;
                            command.Parameters["OUT_CURSOR2"].Direction = ParameterDirection.Output;

                            DataSet ds = new DataSet();
                            (new OracleDataAdapter(command)).Fill(ds);
                            dtSever = ds.Tables[0];
                            dtDept = ds.Tables[1];
                        }
                    }

                    if (dtSever == null || dtSever.Rows.Count == 0)
                    {
                        writeLog("No Data!");
                        return;
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End get data");

                    int iRowUpload = 0, iRow = dtSever.Rows.Count;

                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Begin tUser: " + iRow.ToString());

                    //Get empid in table tUser
                    using (OleDbDataAdapter da = new OleDbDataAdapter("SELECT ID, idno FROM TUSER ", con))
                    {
                        da.Fill(dtLocal);
                    }

                    foreach (DataRow row in dtSever.Rows)
                    {
                        if (dtLocal.Select("ID = '" + row["EMP_NO"] + "'").Count() == 0)
                        {
                            //Insert table tUser
                            strSql = string.Format("INSERT INTO TUSER( ID,             Name,        CARDNUM,              " +
                                                                     " DEPT,           group_id,     REG_DATE,  " +
                                                                     " DATELIMIT,      VALIDTYPE,  PADMIN,    " +
                                                                     " VALID_YN,       idno) " +
                                                             " VALUES( '{0}', '{1}', '{2}', " +
                                                                     " '{3}', '{4}', '{6}', " +
                                                                     " '{7}',  '3',  7, " +
                                                                     "  'Y',  '{5}')",
                                                row["EMP_NO"], row["ENG_NAME"], row["RF_ID"],
                                                row["DEP_NAME"], row["DEP_DIV"], row["EMPID"],
                                                row["REG_DATE"], row["DATE_LIMIT"]);
                            execAccess(strSql, con, "  Insert tUser-->" + row["EMP_NO"]);


                        }
                        else
                        {
                            strSql = string.Format("UPDATE TUSER " +
                                                     "   SET VALID_YN = 'Y'   " +
                                                     "     , NAME = '{1}'" +
                                                     "     , CARDNUM = '{2}'" +
                                                     "     , DEPT = '{3}'" +
                                                     "     , REG_DATE = '{6}'" +
                                                     "     , DATELIMIT = '{7}'" +
                                                     "     , VALIDTYPE = '4'" +
                                                     "     , PADMIN = 7" +
                                                     "     , group_id = '{4}'" +
                                                     " WHERE ID = {0}",
                                                     row["EMP_NO"], row["ENG_NAME"], row["RF_ID"],
                                                     row["DEP_NAME"], row["DEP_DIV"], row["EMPID"],
                                                     row["REG_DATE"], row["DATE_LIMIT"]);
                            execAccess(strSql, con, "  Update tUser-->" + row["EMP_NO"]);
                        }

                        lblStatus.Text = "Upload: " + (++iRowUpload).ToString() + "/" + iRow.ToString();
                    }

                    lblStatus.Text = "";
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->End tUser");
                }

            }
            catch (Exception ex)
            {
                writeLog("  Error !!!");
                Lib.ClassLib.writeToLog("uploadUNIS : " + ex.ToString());
            }
            finally
            {
                cmdStart.Enabled = true;
                writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Finish Download ");
            }
        }
        #endregion

        #region SQL
        [Obsolete]
        private void downloadSQL()
        {
            writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Start Download");

            string strSever = cboSever.SelectedValue.ToString();
            string strLocal = cboLocal.SelectedValue.ToString();
            string strUser = System.Net.Dns.GetHostName();
            string strIP = System.Net.Dns.GetHostEntry(strUser).AddressList.GetValue(1).ToString();
            string strSql = "";
            string[] arrVar;
            DataTable dtSql1 = new DataTable();
            DataTable dtSql2 = new DataTable();
            DataTable dtDept = new DataTable();
            DataTable dtSever = new DataTable();


            using (SqlConnection conSql = new SqlConnection(strLocal))
            {
                conSql.Open();
                using (OracleConnection conOra = new OracleConnection(strSever))
                {
                    conOra.Open();
                    using (OracleCommand command = new OracleCommand("PW_TL_SCAN_MC_DOWN_SQL_S", conOra))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("ARG_DATA", OracleType.VarChar).Value = _strData;
                        command.Parameters.Add("ARG_NAME_DB", OracleType.VarChar).Value = cboLocal.Text;
                        command.Parameters.Add("ARG_IP", OracleType.VarChar).Value = strIP;
                        command.Parameters.Add("ARG_USER", OracleType.VarChar).Value = strUser;
                        command.Parameters.Add("OUT_CURSOR", OracleType.Cursor);
                        command.Parameters.Add("OUT_CURSOR2", OracleType.Cursor);

                        command.Parameters["ARG_DATA"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_NAME_DB"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_IP"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_USER"].Direction = ParameterDirection.Input;
                        command.Parameters["OUT_CURSOR"].Direction = ParameterDirection.Output;
                        command.Parameters["OUT_CURSOR2"].Direction = ParameterDirection.Output;

                        DataSet ds = new DataSet();
                        (new OracleDataAdapter(command)).Fill(ds);
                        dtSql1 = ds.Tables[0];
                        dtSql2 = ds.Tables[1];
                    }

                    DataTable dtLocal = _db.getDataSQL(dtSql1.Rows[0]["SQL_TEXT1"].ToString(), conSql, "Get Emp in SQL DB");
                    int iCol = dtLocal.Columns.Count;
                    arrVar = new string[iCol + 1];

                    _db.execOra(string.Format(dtSql1.Rows[0]["SQL_TEXT2"].ToString(), strUser + "|" + strIP), conOra, "Insert Sql to Ora");

                    strSql = dtSql1.Rows[0]["SQL_TEXT3"].ToString();

                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin Ora HE_EM_RF ");
                    foreach (DataRow row in dtLocal.Rows)
                    {
                        for (int i = 0; i < iCol; i++)
                            arrVar[i] = row[i].ToString();
                        arrVar[iCol] = strUser + "|" + strIP;
                        if (_db.execOra(string.Format(strSql, arrVar), conOra, "Insert Sql to Ora") == 0)
                        {
                            writeLog(" Insert Sql to Ora: " + row[0]);
                        }
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End Ora HE_EM_RF ");


                    //Get data Oracle
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin get data Ora");
                    using (OracleCommand command = new OracleCommand("PW_TL_SCAN_MC_DOWNLOAD_S", conOra))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("ARG_DATA", OracleType.VarChar).Value = _strData;
                        command.Parameters.Add("ARG_NAME_DB", OracleType.VarChar).Value = cboLocal.Text;
                        command.Parameters.Add("ARG_IP", OracleType.VarChar).Value = strIP;
                        command.Parameters.Add("ARG_USER", OracleType.VarChar).Value = strUser;
                        command.Parameters.Add("OUT_CURSOR", OracleType.Cursor);
                        command.Parameters.Add("OUT_CURSOR2", OracleType.Cursor);

                        command.Parameters["ARG_DATA"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_NAME_DB"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_IP"].Direction = ParameterDirection.Input;
                        command.Parameters["ARG_USER"].Direction = ParameterDirection.Input;
                        command.Parameters["OUT_CURSOR"].Direction = ParameterDirection.Output;
                        command.Parameters["OUT_CURSOR2"].Direction = ParameterDirection.Output;

                        DataSet ds = new DataSet();
                        (new OracleDataAdapter(command)).Fill(ds);
                        dtSever = ds.Tables[0];
                        dtDept = ds.Tables[1];
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End get data Ora");

                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin insert emp");
                    foreach (DataRow row in dtSever.Rows)
                    {
                        _db.execSql(row["SQL_CODE"].ToString(), conSql, row["SQL_CODE"].ToString());
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End insert emp");

                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->Begin insert dept");
                    foreach (DataRow row in dtDept.Rows)
                    {
                        _db.execSql(row["SQL_CODE"].ToString(), conSql, row["SQL_CODE"].ToString());
                    }
                    writeLog("  " + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "-->End insert dept");
                }

            }          
            writeLog(DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss") + "--->Finish Download");
        }

        #endregion

       

        #region Proc

        private void execAccess(string strSql, OleDbConnection con, string argErr)
        {
            try
            {
                using (OleDbCommand cmd = new OleDbCommand(strSql, con))
                {
                    if (cmd.ExecuteNonQuery().ToString() == "0")
                    {
                        writeLog(argErr + ": Fail");
                    }                   
                }
            }
            catch (Exception ex)
            {
                writeLog(argErr);
                Lib.ClassLib.writeToLog(argErr + ": " + ex.ToString());
            }
        }

        private void RunDownload()
        {
            ThreadStart ts;

            if (cboLocal.Text == "SQL")
            { ts = new ThreadStart(downloadSQL); }
            else if (cboLocal.Text == "ACCESS")
            { ts = new ThreadStart(downloadACCESS); }
            else
                ts = new ThreadStart(downloadUNIS);

            _thrd = new Thread(ts);
            _thrd.Start();
        }

        private void writeLog(string argText)
        {
           
            txtLog.BeginInvoke(new Action(() =>
            {
                txtLog.Text += argText + "\r\n";
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
                txtLog.Refresh();
            }));
        }

        //private void writeError(string argText)
        //{

        //    txtLog.BeginInvoke(new Action(() =>
        //    {
        //        txtErr.Text += argText + "\r\n";
        //        txtErr.SelectionStart = txtLog.TextLength;
        //        txtErr.ScrollToCaret();
        //        txtErr.Refresh();
        //    }));
        //}

        private void setCbo(ComboBox argCbo, DataTable dt)
        {
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                _dtnTypeDb.Add(dt.Rows[i][0].ToString(), dt.Rows[i][1].ToString());
            }
            argCbo.DataSource = dt;
            argCbo.DisplayMember = "NAME";
            argCbo.ValueMember = "CONNECT";
            argCbo.SelectedIndex = 0;
           // checkDB(argCbo);
        }

        [Obsolete]
        private bool checkDB(ComboBox argCbo)
        {
            if (_dtnTypeDb[argCbo.Text] == "ACCESS" || _dtnTypeDb[argCbo.Text] == "UNIS")
            {
                return _db.checkConnectAccess(argCbo.SelectedValue.ToString());
            }
            else if (_dtnTypeDb[argCbo.Text] == "SQL")
            {
                return _db.checkConnectSql(argCbo.SelectedValue.ToString());
            }
            else 
            {
                return _db.checkConnectOracle(argCbo.SelectedValue.ToString());
            }
        }

        #endregion

        
    }
}
