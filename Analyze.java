package analyze;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.io.FileWriter;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import org.apache.commons.cli.CommandLine;
import org.apache.commons.cli.CommandLineParser;
import org.apache.commons.cli.Options;
import org.apache.commons.cli.PosixParser;
import org.supercsv.io.CsvListReader;
import org.supercsv.io.CsvListWriter;
import org.supercsv.prefs.CsvPreference;


public class Analyze {

    static Pattern CONTINUOUS_PATTERN = Pattern.compile("\\-*(\\d+\\.*\\d*)");
    static int categoricalCutoff = 100;

    static class FieldStats {

        String fieldname = null;
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
        
        double numericalSum = 0;
        double numericalSumOfSquares = 0;
        boolean categoricalStopped = false;
        Map<String, Integer> categoricalValues = new HashMap<String, Integer>();

        FieldStats(String fieldname, int fieldno) {
            this.fieldname = fieldname;
            this.fieldno = fieldno;
        }

        boolean isContinuous() {
            return this.continuousCount + this.blankCount == this.analyzeCount;
        }

        boolean isCategorical() {
            return !isContinuous() && !categoricalStopped && this.categoricalCount > 0;
        }

        boolean containsBlanks() {
            return this.blankCount > 0;
        }

        boolean isModelReady() {
            return this.isContinuous() || this.isCategorical();
        }

        int getCategoricalDistinctCount() {
            return this.categoricalValues.values().size();
        }

        boolean canCategoricalStop() {
            if (this.categoricalStopped) {
                return true;
            }
            if (this.getCategoricalDistinctCount() > categoricalCutoff) {
                this.categoricalStopped = true;
                return true;
            }
            return false;
        }

        void addCategoricalValue(String fieldval) {

            Integer count = this.categoricalValues.get(fieldval);
            if (count == null) {
                this.categoricalValues.put(fieldval, 1);
            } else {
                this.categoricalValues.put(fieldval, count + 1);
            }
            this.categoricalCount++;
        }

        void analyzeField(String fieldval, int recno) {
            this.analyzeCount++;
            int _width = fieldval.length();
            if (_width > this.maxWidth) {
                this.maxWidth = _width;
            }

            if (Analyze.isContinuous(fieldval)) {
                this.continuousCount++;
                double fv = Double.parseDouble(fieldval);
                
                this.meanDelta = fv - this.mean;
                this.mean = this.mean + this.meanDelta / this.analyzeCount;
                this.M2 = this.M2 + this.meanDelta * (fv - this.mean);

            } else {
                if (fieldval.equals("")) {
                    this.blankCount++;
                }
                if (!this.canCategoricalStop()) {
                    this.addCategoricalValue(fieldval);
                }
            }
        }
        
        Double getMean() {
            if (!this.isContinuous()) {
                return null;
            }
            return this.mean;
        }
        
        Double getStdDev() {
            if (!this.isContinuous()) {
                return null;
            }
            double variance = this.M2 / (this.analyzeCount - 1);
            return Math.sqrt(variance);
        }

        List<String> getFieldReportRecord() {
            int catcount = 0;
            int catdstcount = 0;
            int contcount = 0;
            if (this.isContinuous()) {
                contcount = this.continuousCount;
            }
            if (this.isCategorical()) {
                catcount = this.categoricalCount;
                catdstcount = this.getCategoricalDistinctCount();
            }


            List<String> rec = new ArrayList<String>();
            rec.add(String.valueOf(this.fieldno));
            rec.add(this.fieldname);
            rec.add(String.valueOf(this.maxWidth));
            rec.add(String.valueOf(this.isContinuous()));
            rec.add(String.valueOf(this.isCategorical()));
            rec.add(String.valueOf(this.analyzeCount));
            rec.add(String.valueOf(this.blankCount));
            rec.add(String.valueOf(contcount));
            rec.add(String.valueOf(catcount));
            rec.add(String.valueOf(catdstcount));
            Double _mean = this.getMean();
            Double _std_dev = this.getStdDev();
            rec.add(_mean == null ? "" : String.valueOf(_mean));
            rec.add(_std_dev == null ? "" : String.valueOf(_std_dev));

            return rec;
        }

        List<List<String>> getFieldValuesReportRecords() {
            if (!this.isCategorical()) {
                return null;
            }
            List<List<String>> recs = new ArrayList<List<String>>();

            for (String val : this.categoricalValues.keySet()) {
                List<String> rec = new ArrayList<String>();
                rec.add(String.valueOf(this.fieldno));
                rec.add(this.fieldname);
                rec.add(val);
                recs.add(rec);
                rec.add(String.valueOf(this.categoricalValues.get(val)));
            }

            return recs;
        }

        static List<String> getFieldReportHeaderRecord() {
            List<String> rec = new ArrayList<String>();
            rec.add("fieldno");
            rec.add("fieldname");
            rec.add("max_width");
            rec.add("is_continuous");
            rec.add("is_categorical");
            rec.add("analyze_count");
            rec.add("blank_count");
            rec.add("continuous_count");
            rec.add("categorical_count");
            rec.add("categorical_distinct_count");
            rec.add("numerical_mean");
            rec.add("numerical_std_dev");
            return rec;
        }

        static List<String> getFieldValueReportHeaderRecord() {
            List<String> rec = new ArrayList<String>();
            rec.add("fieldno");
            rec.add("fieldname");
            rec.add("categorical_value");
            rec.add("categorical_count");
            return rec;
        }
    }

    static boolean isContinuous(String fieldval) {
        if (fieldval == null) {
            return false;
        }
        return CONTINUOUS_PATTERN.matcher(fieldval).matches();
    }

    public static void analyze(List<File> inputFiles, String fieldReport, String fieldValueReport) throws Exception {
        if (inputFiles.isEmpty()) {
            throw new Exception("No input files!");
        }
        File firstInputFile = inputFiles.get(0);
        if (fieldReport == null) {
            fieldReport = firstInputFile.getAbsolutePath() + ".fields.csv";
        }
        if (fieldValueReport == null) {
            fieldValueReport = firstInputFile.getAbsolutePath() + ".fieldvalues.csv";
        }

        System.out.println("Field Report: " + fieldReport);
        System.out.println("Field Value Report:" + fieldValueReport);

        FileWriter fieldReportFileWriter = new FileWriter(fieldReport);
        CsvListWriter fieldReportCsvWriter = new CsvListWriter(fieldReportFileWriter, CsvPreference.EXCEL_PREFERENCE);
        fieldReportCsvWriter.write(FieldStats.getFieldReportHeaderRecord());

        FileWriter fieldValueReportFileWriter = new FileWriter(fieldValueReport);
        CsvListWriter fieldValueReportCsvWriter = new CsvListWriter(fieldValueReportFileWriter, CsvPreference.EXCEL_PREFERENCE);
        fieldValueReportCsvWriter.write(FieldStats.getFieldValueReportHeaderRecord());

        Map<String, FieldStats> statsdb = new HashMap<String, FieldStats>();

        String[] headerFields = null;
        int recno = 0;
        int headerno = 0;
        for (File inputfile : inputFiles) {
            
            System.out.println("Analyzing input file " + 
                    inputfile.getAbsolutePath());
            
            FileReader fileReader = new FileReader(inputfile);
            BufferedReader reader = new BufferedReader(fileReader);
            CsvListReader csvReader = new CsvListReader(reader,
                    CsvPreference.EXCEL_PREFERENCE);

            headerFields = csvReader.getCSVHeader(true);
            headerno++;

            while (true) {
                List<String> rec = csvReader.read();


                if (rec == null) {
                    break;
                }
                recno++;                

                int fieldno = 0;
                for (String fieldname : headerFields) {
                    FieldStats stats = statsdb.get(fieldname);
                    if (stats == null) {
                        stats = new FieldStats(fieldname, fieldno + 1);
                        statsdb.put(fieldname, stats);
                    }
                    String fieldval = rec.get(fieldno);
                    stats.analyzeField(fieldval, recno);
                    fieldno++;
                }
            }
            fileReader.close();
        }

        System.out.println("Header CSV records read: " + headerno);
        System.out.println("Total CSV data records read: " + recno);
        
        for (String fieldname : headerFields) {
            FieldStats stats = statsdb.get(fieldname);
            fieldReportCsvWriter.write(stats.getFieldReportRecord());
            List<List<String>> catValuesRecords = 
                    stats.getFieldValuesReportRecords();
            
            if (catValuesRecords != null) {
                for (List<String> rec : catValuesRecords) {
                    if (rec != null) {
                        fieldValueReportCsvWriter.write(rec);
                    }
                }
            }
        }


        fieldValueReportCsvWriter.close();
        fieldReportCsvWriter.close();

    }

    public static void main(String[] args) throws Exception {


        Options options = new Options();
        options.addOption("", "field-report", true, "Write the field analysis report to this file. If not specified, then {inputfile}.fields.csv will be used.");
        options.addOption("", "field-value-report", true, "Write the field values analysis report to this file. If not specified, then {inputfile}.fieldvalues.csv will be used.");
        
        CommandLineParser parser = new PosixParser();
        CommandLine cmd = parser.parse(options, args);
        String fieldReport = cmd.getOptionValue("field-report");
        String fieldValueReport = cmd.getOptionValue("field-value-report");

        args = cmd.getArgs();
        if (args.length == 0) {
            System.out.println("Analyze the fields in a CSV file or several " + 
                    "related input files (split files with same layout)");
            System.out.println("Specify at least one CSV input file to analyze.");
            System.exit(2);
        }

        List<File> inputFiles = new ArrayList<File>();
        for (int i = 0; i < args.length; i++) {
            File inputFile = new File(args[i]);
            inputFiles.add(inputFile);
        }

        try {
            analyze(inputFiles, fieldReport, fieldValueReport);
        } catch (Exception e) {
            e.printStackTrace(System.err);
            System.exit(1);
        }
    }
}
