using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;

using NDesk.Options;
using net.sf.dotnetcli;

namespace analyze {
     internal static class HashMapGetHelperClass
    {
        internal static TValue GetValueOrNull<TKey, TValue>(this System.Collections.Generic.IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue ret;
            dictionary.TryGetValue(key, out ret);
            return ret;
        }
    }

     internal class Analyze
     {

         static Regex CONTINUOUS_PATTERN = new Regex("\\-*(\\d+\\.*\\d*)");
         static int categoricalCutoff = 100;

         internal class FieldStats
         {

             string fieldname = null;
             int fieldno = -1;
             int continuousCount = 0;
             int categoricalCount = 0;
             int blankCount = 0;
             int analyzeCount = 0;
             int maxWidth = 0;

             //See http://en.wikipedia.org/wiki/Algorithms_for_calculating_variance#Online_algorithm
             double mean = 0;
             double meanDelta = 0;
             double M2 = 0;

             //not used?
             double numericalSum = 0;
             double numericalSumOfSquares = 0;
             bool categoricalStopped = false;

             //TODO
             //Map<String, Integer> categoricalValues = new HashMap<String, Integer>();
             IDictionary<string, int> categoricalValues = new Dictionary<string, int>();


             internal FieldStats(string fieldname, int fieldno)
             {
                 this.fieldname = fieldname;
                 this.fieldno = fieldno;
             }

             bool isContinuous()
             {
                 return this.continuousCount + this.blankCount == this.analyzeCount;
             }

             bool isCategorical()
             {
                 return !isContinuous() && !categoricalStopped && this.categoricalCount > 0;
             }

             bool containsBlanks()
             {
                 return this.blankCount > 0;
             }

             bool isModelReady()
             {
                 return this.isContinuous() || this.isCategorical();
             }

             int getCategoricalDistinctCount()
             {
                 //TODO - need to make sure this is distinct?
                 // return this.categoricalValues.values().size();
                 return this.categoricalValues.Count;
             }

             bool canCategoricalStop()
             {
                 if (this.categoricalStopped)
                 {
                     return true;
                 }
                 if (this.getCategoricalDistinctCount() > categoricalCutoff)
                 {
                     this.categoricalStopped = true;
                     return true;
                 }
                 return false;
             }

             void addCategoricalValue(string fieldval)
             {

                 int count = this.categoricalValues.GetValueOrNull(fieldval);
                 if (count < 1)
                 {
                     this.categoricalValues[fieldval] = 1;
                 }
                 else
                 {
                     this.categoricalValues[fieldval] = count + 1;
                 }
                 this.categoricalCount++;
             }
             internal void analyzeField(string fieldval, int recno)
             {
                 this.analyzeCount++;
                 int _width = fieldval.Length;
                 if (_width > this.maxWidth)
                 {
                     this.maxWidth = _width;
                 }

                 if (Analyze.isContinuous(fieldval))
                 {
                     this.continuousCount++;
                     double fv = Convert.ToDouble(fieldval);

                     this.meanDelta = fv - this.mean;
                     this.mean = this.mean + this.meanDelta / this.analyzeCount;
                     this.M2 = this.M2 + this.meanDelta * (fv - this.mean);

                 }
                 else
                 {
                     if (fieldval == "")
                     {
                         this.blankCount++;
                     }
                     if (!this.canCategoricalStop())
                     {
                         this.addCategoricalValue(fieldval);
                     }
                 }
             }
             double getMean()
             {
                 if (!this.isContinuous())
                 {
                     //TODO return -1 instead of NULL for double type?
                     return -1;
                 }
                 return this.mean;
             }

             double getStdDev()
             {
                 if (!this.isContinuous())
                 {
                     //TODO return -1 instead of NULL for double type?
                     return -1;
                 }
                 double variance = this.M2 / (this.analyzeCount - 1);
                 return Math.Sqrt(variance);
             }
             internal IList<string> getFieldReportRecord()
             {
                 int catcount = 0;
                 int catdstcount = 0;
                 int contcount = 0;
                 if (this.isContinuous())
                 {
                     contcount = this.continuousCount;
                 }
                 if (this.isCategorical())
                 {
                     catcount = this.categoricalCount;
                     catdstcount = this.getCategoricalDistinctCount();
                 }


                 IList<string> rec = new List<string>();
                 rec.Add(this.fieldno.ToString());
                 rec.Add(this.fieldname);
                 rec.Add(this.maxWidth.ToString());
                 rec.Add(this.isContinuous().ToString());
                 rec.Add(this.isCategorical().ToString());
                 rec.Add(this.analyzeCount.ToString());
                 rec.Add(this.blankCount.ToString());
                 rec.Add(contcount.ToString());
                 rec.Add(catcount.ToString());
                 rec.Add(catdstcount.ToString());
                 double _mean = this.getMean();
                 double _std_dev = this.getStdDev();
                 //TODO using -1 instead of null
                 rec.Add(_mean == -1 ? "" : _mean.ToString());
                 rec.Add(_std_dev == -1 ? "" : _std_dev.ToString());

                 return rec;
             }

             internal IList<IList<string>> getFieldValuesReportRecords()
             {
                 if (!this.isCategorical())
                 {
                     return null;
                 }
                 IList<IList<string>> recs = new List<IList<string>>();

                 foreach (string val in this.categoricalValues.Keys)
                 {
                     IList<string> rec = new List<string>();
                     rec.Add(this.fieldno.ToString());
                     rec.Add(this.fieldname);
                     rec.Add(val);
                     recs.Add(rec);
                     rec.Add(Convert.ToString(this.categoricalValues.GetValueOrNull(val)));
                 }

                 return recs;
             }

             internal static IList<string> getFieldReportHeaderRecord()
             {
                 IList<string> rec = new List<string>();
                 rec.Add("fieldno");
                 rec.Add("fieldname");
                 rec.Add("max_width");
                 rec.Add("is_continuous");
                 rec.Add("is_categorical");
                 rec.Add("analyze_count");
                 rec.Add("blank_count");
                 rec.Add("continuous_count");
                 rec.Add("categorical_count");
                 rec.Add("categorical_distinct_count");
                 rec.Add("numerical_mean");
                 rec.Add("numerical_std_dev");
                 return rec;
             }
             internal static IList<string> getFieldValueReportHeaderRecord()
             {
                 IList<string> rec = new List<string>();
                 rec.Add("fieldno");
                 rec.Add("fieldname");
                 rec.Add("categorical_value");
                 rec.Add("categorical_count");
                 return rec;
             }
         }
         static bool isContinuous(string fieldval)
         {
             if (fieldval == null)
             {
                 return false;
             }
             //TODO
             return CONTINUOUS_PATTERN.Match(fieldval).Success;
             
             
         }


         internal static void analyze(IList<FileInfo> inputFiles, string fieldReport, string fieldValueReport)
         {

             try
             {

                 if (inputFiles.Count <= 0)
                 {
                     throw new Exception("No input files!");
                 }
                 FileInfo firstInputFile = inputFiles[0];
                 if (fieldReport == null)
                 {
                     fieldReport = firstInputFile.FullName + ".fields.csv";
                 }
                 if (fieldValueReport == null)
                 {
                     fieldValueReport = firstInputFile.FullName + ".fieldvalues.csv";
                 }

                 Console.WriteLine("Field Report: " + fieldReport);
                 Console.WriteLine("Field Value Report:" + fieldValueReport);

                 //TODO input / output

                 StreamWriter fieldReportFileWriter = new StreamWriter(fieldReport);
                 var fieldReportCsvWriter = new CsvHelper.CsvWriter(fieldReportFileWriter);
                 fieldReportCsvWriter.WriteRecord(FieldStats.getFieldReportHeaderRecord());

                 StreamWriter fieldValueReportFileWriter = new StreamWriter(fieldValueReport);
                 var fieldValueReportCsvWriter = new CsvHelper.CsvWriter(fieldValueReportFileWriter);
                 fieldValueReportCsvWriter.WriteRecord(FieldStats.getFieldValueReportHeaderRecord());

                 IDictionary<string, FieldStats> statsdb = new Dictionary<string, FieldStats>();

                 string[] headerFields = null;
                 int recno = 0;
                 int headerno = 0;
                 
              foreach (FileInfo inputfile in inputFiles) {
            
                 Console.WriteLine("Analyzing input file " + 
                         inputfile.FullName);
                 
                 StreamReader fileReader = new StreamReader(inputfile.FullName);
                 var csvReader = new CsvHelper.CsvReader(fileReader);

                 
                 headerFields = csvReader.FieldHeaders;
                 
                 headerno++;

                 while (csvReader.Read()) {

                     IList<string> rec = csvReader.GetRecord<IList<string>>();

                     if (rec == null) {
                         break;
                     }
                     recno++;                

                     int fieldno = 0;
                     foreach (string fieldname in headerFields) {
                         FieldStats stats = statsdb.GetValueOrNull(fieldname);
                         if (stats == null) {
                             stats = new FieldStats(fieldname, fieldno + 1);
                             statsdb[fieldname] = stats;
                         }
                         string fieldval = rec[fieldno];
                         stats.analyzeField(fieldval, recno);
                         fieldno++;
                     }
                 }
                 fileReader.Close();
              
              }

             Console.WriteLine("Header CSV records read: " + headerno);
             Console.WriteLine("Total CSV data records read: " + recno);
             foreach (string fieldname in headerFields) {
                 FieldStats stats = statsdb.GetValueOrNull(fieldname);
                 fieldReportCsvWriter.WriteRecord(stats.getFieldReportRecord());
                 IList<IList<string>> catValuesRecords = 
                         stats.getFieldValuesReportRecords();
            
                 if (catValuesRecords != null) {
                     foreach (IList<string> rec in catValuesRecords) {
                         if (rec != null) {
                             fieldValueReportCsvWriter.WriteRecord(rec);
                         }
                     }
                 }
             }

             fieldValueReportCsvWriter.Dispose();
             fieldReportCsvWriter.Dispose();
            
               
             }

             catch (Exception e)
             {
                 //TODO not sure how we wanted to handle errors - leaving this as an easy way to toggle on/off
                 throw;
             }
         }
         public static void Main(string[] args)
         {
             try
             {


                 Options options = new Options();
                 options.AddOption("1", "field-report", true, "Write the field analysis report to this file. If not specified, then {inputfile}.fields.csv will be used.");
                 options.AddOption("2", "field-value-report", true, "Write the field values analysis report to this file. If not specified, then {inputfile}.fieldvalues.csv will be used.");


                 ICommandLineParser parser = new PosixParser();
                 CommandLine cmd = parser.Parse(options, args);
                 string fieldReport = cmd.GetOptionValue("field-report");
                 string fieldValueReport = cmd.GetOptionValue("field-value-report");

                 args = cmd.Args;
                 if (args.Length == 0)
                 {
                     Console.WriteLine("Analyze the fields in a CSV file or several " + "related input files (split files with same layout)");
                     Console.WriteLine("Specify at least one CSV input file to analyze.");
                     Environment.Exit(2);
                 }


                 IList<FileInfo> inputFiles = new List<FileInfo>();
                 for (int i = 0; i < args.Length; i++)
                 {
                     FileInfo inputFile = new FileInfo(args[i]);
                     inputFiles.Add(inputFile);
                 }
                 
                 try
                 {
                     analyze(inputFiles, fieldReport, fieldValueReport);
                     
                 }
                 catch (Exception e)
                 {
                     Console.WriteLine(e.Message);
                     Environment.Exit(1);
                 }
             }

             catch (Exception e)
             {
                 //TODO not sure how we wanted to handle errors - leaving this as an easy way to toggle on/off
                 throw;
             }
         }
     }
}