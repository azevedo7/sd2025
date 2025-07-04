﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

  

    class CsvHelper
    {
        public static void UpdateWavyStatus(string wavyId, string newStatus)
        {
            string csvFilePath = "wavy.csv";

            if (!File.Exists(csvFilePath))
            {
                Console.WriteLine("CSV file does not exist.");
                return;
            }

            List<string> csvLines = File.ReadAllLines(csvFilePath).ToList();
            bool found = false;

            for (int i = 0; i < csvLines.Count; i++)
            {
                var columns = csvLines[i].Split(',');

                if (columns[0] == wavyId)
                {
                    columns[1] = newStatus;
                    columns[3] = DateTime.UtcNow.ToString("o"); // update last sync
                    csvLines[i] = string.Join(",", columns);
                    found = true;
                    Console.WriteLine($"Updated status for {wavyId} to {newStatus}");
                    break;
                }
            }

            if (found)
            {
                try
                {
                    File.WriteAllLines(csvFilePath, csvLines);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error writing to CSV: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine($"WAVY ID {wavyId} not found in CSV. No update performed.");
            }
        }

        // Data comes in this JSON format to aggregator
        //{
        //    "wavyId": "WAVY_123",
        //    "dataType": "temperature",
        //    "value": 25.5
        //}
        public void SaveData(string wavyId, string dataType, string data)
    {
        string csvFilePath = $"{dataType}.csv";
        string wavyCsvFilePath = "wavy.csv";
        try
        {
            string now = DateTime.UtcNow.ToString("o");
            using (StreamWriter sw = new StreamWriter(csvFilePath, true))
            {
                sw.WriteLine($"{wavyId},{data},{now}");
            }
            Console.WriteLine($"Data saved to {csvFilePath}");

            if (File.Exists(wavyCsvFilePath))
            {
                List<string> csvLines = File.ReadAllLines(wavyCsvFilePath).ToList();
                bool found = false;

                for (int i = 0; i < csvLines.Count; i++)
                {
                    var columns = csvLines[i].Split(',');

                    if (columns[0] == wavyId)
                    {
                        found = true;
                        var dataTypes = columns[2] == "[]" ? new List<string>() : columns[2].Trim('[', ']').Split(';').ToList();
                        if (!dataTypes.Contains(dataType))
                        {
                            dataTypes.Add(dataType);
                            columns[2] = $"[{string.Join(";", dataTypes)}]";
                            csvLines[i] = string.Join(",", columns);
                            File.WriteAllLines(wavyCsvFilePath, csvLines);
                            Console.WriteLine($"Added data type {dataType} for {wavyId} in wavy.csv");
                        }
                        break;
                    }
                }

                if (!found)
                {
                    Console.WriteLine($"WAVY ID {wavyId} not found in wavy.csv. No data type update performed.");
                }
            }
            else
            {
                Console.WriteLine("wavy.csv file does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error writing to CSV: " + ex.Message);
        }
    }
    }

