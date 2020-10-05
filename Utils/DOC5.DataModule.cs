using Match_Verify.DataModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace DOC5
{
    public static class DataModule
    {
        public static bool ExecSP_THESAURUS_PERFORMER_S(SqlConnection conn, string performerLink, out DataSet ds)
        {
            ds = null;
            try
            {
                using SqlCommand command = new SqlCommand
                {
                    Connection = conn,
                    CommandType = CommandType.StoredProcedure,
                    CommandText = "THESAURUS_PERFORMER_S"
                };
                command.Parameters.Add("@AUTHOR_ID", SqlDbType.BigInt, 0).Value = DOC5.ConnectionInfo.AUTHOR_ID;
                command.Parameters.Add("@DEVICE_ID", SqlDbType.BigInt, 0).Value = DOC5.ConnectionInfo.DEVICE_ID;
                command.Parameters.Add("@PERFORMER_ID", SqlDbType.BigInt, 0).Value = DBNull.Value;
                command.Parameters.Add("@PERFORMER_LINK", SqlDbType.NVarChar, 12).Value = performerLink;

                SqlDataAdapter adapter = new SqlDataAdapter(command);
                ds = new DataSet();
                adapter.Fill(ds);

                return ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0;
            }
            catch (Exception e)
            {
                Log.Logger.Information(e.ToString());
            }

            return false;
        }

        public static bool ExecSP_PERFORMER_LOOKUP_IDENTIFIER_IU(SqlConnection conn, long performerId, LinkRecord link)
        {
            try
            {
                using SqlCommand command = new SqlCommand
                {
                    Connection = conn,
                    CommandType = CommandType.StoredProcedure,
                    CommandText = "PERFORMER_LOOKUP_IDENTIFIER_IU"
                };

                command.Parameters.Add("@AUTHOR_ID", SqlDbType.BigInt, 0).Value = DOC5.ConnectionInfo.AUTHOR_ID;
                command.Parameters.Add("@DEVICE_ID", SqlDbType.BigInt, 0).Value = DOC5.ConnectionInfo.DEVICE_ID;
                command.Parameters.Add("@PERFORMER_LOOKUP_IDENTIFIER_ID", SqlDbType.BigInt, 0).Direction = ParameterDirection.InputOutput;
                command.Parameters["@PERFORMER_LOOKUP_IDENTIFIER_ID"].Value = DBNull.Value;
                command.Parameters.Add("@PERFORMER_ID", SqlDbType.BigInt, 24).Value = performerId;
                command.Parameters.Add("@DATALABEL_CODE", SqlDbType.NVarChar, 24).Value = link.Source;
                command.Parameters.Add("@VALUE", SqlDbType.NVarChar, 256).Value = link.Url;
                command.Parameters.Add("@MANUAL_OVERRIDE", SqlDbType.Bit, 0).Value = false;
                command.Parameters.Add("@PRIORITY", SqlDbType.SmallInt, 0).Value = DBNull.Value;
                command.Parameters.Add("@OPTIONAL_INFO", SqlDbType.NVarChar, 80).Value = DBNull.Value;
                command.Parameters.Add("@UPDATE_DATE_CHANGED", SqlDbType.Bit, 0).Value = false;

                command.ExecuteNonQuery();
                return true;
            }
            catch (Exception e)
            {
                Log.Logger.Information(e.ToString());
            }

            return false;
        }
    }
}
