﻿using System;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pokladna
{
    public static class Methods
    {
        public static string ConnectionString(string file)
        {
            return String.Format("Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0}", file);
        }

        public static void CreateDatabase(string file)
        {
            // delete old file
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {

                    Log.WriteErrorLog("Cannot delete old database file: " + e.Message);
                }
            }

            // create new file
            Stream objStream = null;
            FileStream objFileStream = null;
            try
            {
                System.Reflection.Assembly objAssembly =
                    System.Reflection.Assembly.GetExecutingAssembly();
                objStream =
                    objAssembly.GetManifestResourceStream("Pokladna.PokladnaToPohoda.mdb");
                byte[] abytResource = new Byte[objStream.Length];
                objStream.Read(abytResource, 0, (int)objStream.Length);
                objFileStream = new FileStream(file, FileMode.Create);
                objFileStream.Write(abytResource, 0, (int)objStream.Length);
                objFileStream.Close();

                Log.WriteLog("Database created: " + file);
            }
            catch (Exception e)
            {
                Log.WriteErrorLog("Cannot create new database file: " + e.Message);
            }
            finally
            {
                if (objFileStream != null)
                {
                    objFileStream.Close();
                    objFileStream = null;
                }
            }
        }

        public static DataSet GetData(string file, string tableName, int month)
        {
            string query = 
                String.Format(
                    "SELECT [Id], [Datum], [CisloDokladu], [Popis], [Pohyb], " +
                    "Format([Castka], 'Currency') AS Castka, " +
                    " '16P0' + Right([CisloDokladu], 4) AS CisloPohoda " +
                    "FROM Data " + 
                    "WHERE CisloDokladu LIKE '{0}%' " +
                    "AND DatePart('m', [Datum]) = {1} ", tableName.Substring(0,1), month);

            OleDbConnection connection = new OleDbConnection(Methods.ConnectionString(file));
            DataSet dataSet = new DataSet();

            try
            {
                using (var adapter = new OleDbDataAdapter(query, connection))
                {
                    adapter.TableMappings.Add("Table", tableName);
                    adapter.Fill(dataSet);
                    Log.WriteLog(String.Format("Data received from the file {0} successfully", file));
                }

            }
            catch (Exception e)
            {
                Log.WriteErrorLog(String.Format("Cannot retrieve data from database file {0}", file) + e.Message);
            }

            return SetDataRowsAdded(dataSet, tableName);
        }

        public static int GetMonth()
        {
            bool process = false;
            int month = 0;

            while (!process)
            {
                try
                {
                    Console.WriteLine("Zadej cislo mesice, ktery chces zpracovat: ");
                    month = Int16.Parse(Console.ReadLine());

                    if (month < 1 || month > 12)
                        Log.WriteWarningLog("Cislo musi byt v rozmezi 1-12!");
                    else
                        process = true;
                }
                catch (Exception)
                {
                    Log.WriteWarningLog("Zadany vstup neni cele cislo!");
                }
            }

            return month;
        }

        public static void InsertData(string file, string tableName, DataSet dataSet)
        {
            string insertString = String.Format("INSERT INTO {0}([Id], [Datum], [Cislo], [PDoklad], [SText], [Pohyb], [Kc0]) VALUES " + 
                "(?, ?, ?, ?, ?, ?, ?);", tableName); // 6 parameters

            try
            {
                using (var connection = new OleDbConnection(ConnectionString(file)))
                {
                    var adapter = new OleDbDataAdapter();
                    adapter.InsertCommand = new OleDbCommand(insertString, connection);
                    adapter.InsertCommand.Parameters.Add("@Id", OleDbType.Integer, 0, "Id");
                    adapter.InsertCommand.Parameters.Add("@Datum", OleDbType.DBDate, 0, "Datum");
                    adapter.InsertCommand.Parameters.Add("@CisloPohoda", OleDbType.VarWChar, 255, "CisloPohoda");
                    adapter.InsertCommand.Parameters.Add("@CisloDokladu", OleDbType.VarWChar, 7, "CisloDokladu");
                    adapter.InsertCommand.Parameters.Add("@Popis", OleDbType.VarWChar, 100, "Popis");
                    adapter.InsertCommand.Parameters.Add("@Pohyb", OleDbType.Integer, 0, "Pohyb");
                    adapter.InsertCommand.Parameters.Add("@Castka", OleDbType.Currency, 0, "Castka");

                    connection.Open();
                    try
                    {
                        adapter.Update(dataSet, tableName);
                        Log.WriteLog(String.Format("Data inserted into destination database file {0} table {1}", file, tableName));
                    }
                    catch (Exception e)
                    {

                        Log.WriteErrorLog(String.Format("Cannot insert data into destination database file {0}: ", file) + e.Message);
                    }
                }
           
            }
            catch (Exception e)
            {
                Log.WriteErrorLog("Database connection error: " + e.Message);
            }
        }

        public static DataSet SetDataRowsAdded(DataSet dataSet, string tableName)
        {
            foreach (DataRow row in dataSet.Tables[tableName].Rows)
            {
                row.SetAdded();
            }

            return dataSet;            
        }
    }
}
