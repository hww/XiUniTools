using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using XiDebugMenu;

namespace XiUnityTools
{
    public static class MemoryProfiler
    {
        private static List<Record> records;
        private static List<Record> reports;
        private static MemoryProfilable profilable;
        private static int RebootCnt;
        public static bool Enabled;
        public static float SampleTime;
        public static int version;

        private static float RebootTime;
        private static float sampleTimer;
        public static float LastSampleTime;
        public static string filePath;
        public static bool writeTofile;
        public static bool writeMessages;

        public static void Initialize(MemoryProfilerConfig config)
        {
            sampleTimer = 0;
            Enabled = config.enable;
            SampleTime = config.sampleTime;
            writeTofile = config.writeTofile;
            writeMessages = config.writeMessages;
            if (writeTofile)
                CreateLogFile();
            LastSampleTime = 0;
            version++;
            if (!Enabled) return;
            records = new List<Record>(1000);
            reports = new List<Record>(100);
            RebootTime = Time.unscaledTime;
            RecordSample();
        }



        public static void Reboot()
        {
            if (!Enabled) return;

            if (writeTofile)
                CreateLogFile();
            records.Clear();
            reports.Clear();
            LastSampleTime = 0;
            sampleTimer = 0;
            RebootTime = Time.time;
            RebootCnt++;
            version++;
            RecordSample();
        }
        static int entityManVers;
        static int represManVers;
        static int targTexVersion;
        public static void OnUpdate()
        {
            sampleTimer += Time.unscaledDeltaTime;
            if (sampleTimer > SampleTime)
            {
                sampleTimer -= SampleTime;
                RecordSample();
            }
        }

        public static void RecordSample()
        {
            var record = new Record();
            UpdateRecord(record);
            LastSampleTime = record.time;
            // -- ....
            /* Add this record */
            records.Add(record);

            if (writeTofile)
                WriteLog(record);
            version++;
        }
        public static void UpdateRecord(Record record)
        { 
            record.time = Time.unscaledTime;

            // -- DRIVER
            /* Returns the amount of allocated memory for the graphics driver, in bytes. */
            record.AllocatedMemoryForGraphicsDriver = Profiler.GetAllocatedMemoryForGraphicsDriver();

            // -- MONO 
            /* Returns the size of the reserved space for managed-memory. */
            record.MonoHeapSize = Profiler.GetMonoHeapSizeLong();
            /*	The allocated managed-memory for live objects and non-collected objects. */
            record.MonoUsedSize = Profiler.GetMonoUsedSizeLong();
            
            // -- OBJECT 
            /* Gathers the native-memory used by a Unity object. */
            //record.RuntimeMemorySize = Profiler.GetRuntimeMemorySizeLong(???);
            
            // -- OTHER
            /* Returns the size of the temp allocator. */
            record.TempAllocatorSize = Profiler.GetTempAllocatorSize();
            /* The total memory allocated by the internal allocators in Unity. Unity reserves large pools of memory from the system. This function returns the amount of used memory in those pools.*/
            record.TotalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
            /* The total memory Unity has reserved.*/
            record.TotalReservedMemory = Profiler.GetTotalReservedMemoryLong();
            /* Unity allocates memory in pools for usage when unity needs to allocate memory. This function returns the amount of unused memory in these pools.*/
            record.TotalUnusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong();
            

        }

        private static void BuildReportTable(int columnsNum)
        {
            if (columnsNum > 100) throw new System.Exception();

            reports.Clear();
            if (records.Count == 0) return;

            var samplesPerColumn = 0;
            if (records.Count <= columnsNum)
            {
                columnsNum = records.Count;
                samplesPerColumn = 1;
            }
            else
            {
                samplesPerColumn = records.Count / columnsNum;
            }
            var lastColumn = columnsNum - 1;
            for (var colIdx=0; colIdx< columnsNum; colIdx++)
            {
                var isLastCol = colIdx == lastColumn;
                var chunkStart = colIdx * samplesPerColumn;
                var chunkEnd = isLastCol ? records.Count : chunkStart + samplesPerColumn;
                var a = new Record();
                for (var j = chunkStart; j < chunkEnd; j++)
                {
                    var b = records[j];
                    if (j == chunkStart)
                        a.time = b.time;
                    a.AllocatedMemoryForGraphicsDriver = Max(a.AllocatedMemoryForGraphicsDriver, b.AllocatedMemoryForGraphicsDriver);
                    a.MonoHeapSize = Max(a.MonoHeapSize, b.MonoHeapSize);
                    a.MonoUsedSize = Max(a.MonoUsedSize, b.MonoUsedSize);
                    a.RuntimeMemorySize = Max(a.RuntimeMemorySize, b.RuntimeMemorySize);
                    a.TempAllocatorSize = Max(a.TempAllocatorSize, b.TempAllocatorSize);
                    a.TotalAllocatedMemory = Max(a.TotalAllocatedMemory, b.TotalAllocatedMemory);
                    a.TotalReservedMemory = Max(a.TotalReservedMemory, b.TotalReservedMemory);
                    a.TotalUnusedReservedMemory = Max(a.TotalUnusedReservedMemory, b.TotalUnusedReservedMemory);
                }
                reports.Add(a);
            }
            // now compute sum and difference
            for (var i=0; i<reports.Count; i++)
                reports[i].UpdateTotal();
        }

        private static long Max(long a, long b)
        {
            return a > b ? a : b;
        }

        private static readonly string TITLE_FORMAT = "{0, -24}";
        private static readonly string COLUMN_FORMAT = "{0, 16}";
        static Record sRecord = new Record();
        public static string BuildReport(int columnsNum = 5)
        {
            if (Enabled)
            {
                BuildReportTable(columnsNum);
                if (reports.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("Version:         {0}", version));
                    sb.AppendLine(string.Format("Profiler Enabled:{0}", Profiler.enabled));
                    sb.AppendLine(string.Format("Reboot Time:     {0}", FormatTime(RebootTime)));
                    sb.AppendLine(string.Format("Last Samp. Time: {0}", FormatTime(LastSampleTime)));
                    sb.AppendLine(string.Format("Reboots Cnt:     {0}", RebootCnt));
                    sb.AppendLine(string.Format("Samples Cnt:     {0}", records.Count));
                    // Inspect all profilable items
                    var item = profilable;
                    while (item != null)
                    {
                        sb.AppendLine(string.Format(">>> {0}", item.name));
                        sb.Append(item.InspectAllShort());
                        item = item.sibling;
                    }
                    UpdateRecord(sRecord);
                    sb.AppendLine(">>> Current");
                    for (var f = 0; f < Record.FIELDS_COUNT; f++)
                    {
                        if (Record.GetEnabled(f))
                        {
                            sb.Append(string.Format(TITLE_FORMAT, Record.GetTitle(f)));
                            sb.AppendLine(string.Format(COLUMN_FORMAT, sRecord.GetField(f)));
                        }
                    }
                    sb.AppendLine(">>> Log");
                    for (var f = 0; f < Record.FIELDS_COUNT; f++)
                    {
                        if (Record.GetEnabled(f))
                        {
                            // LINE 1
                            sb.Append(string.Format(TITLE_FORMAT, Record.GetTitle(f)));
                            for (var i = 0; i < reports.Count; i++)
                                sb.Append(string.Format(COLUMN_FORMAT, reports[i].GetField(f)));
                            sb.AppendLine();

                            // LINE 2
                            sb.Append(string.Format(TITLE_FORMAT, ""));
                            for (var i = 0; i < reports.Count; i++)
                            {
                                if (i == 0)
                                    sb.Append(string.Format(COLUMN_FORMAT, ""));
                                else
                                    sb.Append(string.Format(COLUMN_FORMAT, reports[i].GetDifference(f, reports[i - 1])));

                            }
                            
                            sb.AppendLine();
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine($"The game records the memory state to a buffer each {FormatTime(SampleTime)}secs.");
                    sb.AppendLine($"Display this buffer as ({5}) columns, with maximum values and difference with previous column.");
                    return sb.ToString();
                }
                return string.Empty;
            }
            else
            {
                return "PROFILER DISABLED";
            }
        }

        private static string FormatTime(float time)
        {
            var timeInSeconds = Mathf.RoundToInt(time);
            var seconds = timeInSeconds % 60;
            var timeInMinutes = (timeInSeconds - seconds) / 60;
            var minutes = timeInMinutes % 60;
            var hours = (timeInMinutes - minutes) / 60;
            return string.Format("{0}:{1}:{2}", hours, minutes, seconds);
        }

        public static void WriteMessage(string message)
        {
            if (Enabled && writeTofile && writeMessages)
            {
                using (var writer = new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write)))
                {
                    // do work here.
                    var addComa = false;
                    for (var i = 0; i < Record.FIELDS_COUNT; i++)
                    {
                        if (Record.GetEnabled(i))
                        {
                            if (addComa)
                                writer.Write('\t');
                            addComa = true;
                        }
                    }
                    writer.Write('\t');
                    writer.WriteLine(message);
                    writer.WriteLine();
                }
            }
        }

        // ======================================================================================================
        // LOG FILE
        // ======================================================================================================

        private static void CreateLogFile()
        {
            var tag = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = $"{Application.persistentDataPath}/memory_{tag}.txt";
            Debug.Log($"MemoryProfiler: created memory log file '{filePath}'");
            WriteLogHeader();
        }
        private static void WriteLogHeader()
        {
            using (var writer = new StreamWriter(File.Create(filePath)))
            {
                writer.WriteLine($"Momory Profiler");
                writer.WriteLine($"  enabled by shift-E menu: {Enabled}");
                writer.WriteLine($"  write to file: {writeTofile}");
                writer.WriteLine($"  sample period: {SampleTime}");
                writer.WriteLine($"  write additional messages: {writeMessages}");

                var addComa = false;
                for (var i = 0; i < Record.FIELDS_COUNT; i++)
                {
                    if (Record.GetEnabled(i))
                    {
                        if (addComa)
                            writer.Write('\t');

                        writer.Write(Record.GetTitle(i));
                        addComa = true;
                    }
                }
                writer.Write("\tMessage");
                writer.WriteLine();
            }
        }
        private static void WriteLog(Record record)
        {
            using (var writer = new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write)))
            {
                // do work here.
                var addComa = false;
                for (var i = 0; i < Record.FIELDS_COUNT; i++)
                {
                    if (Record.GetEnabled(i))
                    {
                        if (addComa)
                            writer.Write('\t');


                        writer.Write(record.GetField(i));
                        addComa = true;
                    }
                }
                writer.Write('\t');
                writer.WriteLine();
            }

        }

        // ======================================================================================================
        // Log Record
        // ======================================================================================================

        public class Record
        {
            public float time;
            public long AllocatedMemoryForGraphicsDriver;
            public long MonoHeapSize;
            public long MonoUsedSize;
            public long RuntimeMemorySize;
            public long TempAllocatorSize;
            public long TotalAllocatedMemory;
            public long TotalReservedMemory;
            public long TotalUnusedReservedMemory;
            public long Total;

            public const int FIELDS_COUNT = 10;

            public static bool GetEnabled(int field)
            {
                switch (field)
                {
                    case 0: return true;
                    case 1: return true;
                    case 2: return true;
                    case 3: return true;
                    case 4: return true;
                    case 5: return true;
                    case 6: return true;
                    case 7: return true;
                    case 8: return false;
                    case 9: return true;
                }
                return false;
            }

            public static string GetTitle(int field)
            {
                switch (field)
                {
                    case 0: return "Time";
                    case 1: return "Memory For Driver";
                    case 2: return "Mono Heap";
                    case 3: return "Mono Used";
                    case 4: return "Temp Allocator";
                    case 5: return "Total Allocated";
                    case 6: return "Total Reserved";
                    case 7: return "Total Unused Reserved";
                    case 8: return "Runtime Memory Size";
                    case 9: return "Total ->";
                }
                return string.Empty;
            }

            public string GetField(int field)
            {
                switch (field)
                {
                    case 0: return FormatTime(time);
                    case 1: return AllocatedMemoryForGraphicsDriver.ToString("N0");
                    case 2: return MonoHeapSize.ToString("N0");
                    case 3: return MonoUsedSize.ToString("N0");
                    case 4: return TempAllocatorSize.ToString("N0");
                    case 5: return TotalAllocatedMemory.ToString("N0");
                    case 6: return TotalReservedMemory.ToString("N0");
                    case 7: return TotalUnusedReservedMemory.ToString("N0");
                    case 8: return RuntimeMemorySize.ToString("N0");
                    case 9: return Total.ToString("N0");
                }
                return string.Empty;
            }

            public string GetDifference(int field, Record previous)
            {
                switch (field)
                {
                    case 0: return string.Empty;
                    case 1: return FormatDifference(AllocatedMemoryForGraphicsDriver - previous.AllocatedMemoryForGraphicsDriver);
                    case 2: return FormatDifference(MonoHeapSize - previous.MonoHeapSize);
                    case 3: return FormatDifference(MonoUsedSize - previous.MonoUsedSize);
                    case 4: return FormatDifference(TempAllocatorSize - previous.TempAllocatorSize);
                    case 5: return FormatDifference(TotalAllocatedMemory - previous.TotalAllocatedMemory);
                    case 6: return FormatDifference(TotalReservedMemory - previous.TotalReservedMemory);
                    case 7: return FormatDifference(TotalUnusedReservedMemory - previous.TotalUnusedReservedMemory);
                    case 8: return FormatDifference(RuntimeMemorySize - previous.RuntimeMemorySize);
                    case 9: return FormatDifference(Total - previous.Total);
                }
                return string.Empty;
            }

            private static string FormatDifference(long difference)
            {
                if (difference == 0)
                    return string.Empty;
                if (difference > 0)
                    return string.Format("+{0:n0}", difference);
                else
                    return string.Format("{0:n0}", difference);
            }

            public long UpdateTotal()
            {
                Total = 0;
                Total += AllocatedMemoryForGraphicsDriver;
                Total += MonoHeapSize;
                Total += MonoUsedSize;
                Total += RuntimeMemorySize;
                Total += TempAllocatorSize;
                Total += TotalAllocatedMemory;
                Total += TotalReservedMemory;
                Total += TotalUnusedReservedMemory;
                return Total;
            }

        }
    }

    [System.Serializable]
    public class MemoryProfilerConfig
    {
        [Header("Look at streaming access folder")]
        public bool enable = true;           //
        public float sampleTime = 300.0f;    // How often (in seconds) sample memorydata
        public bool writeTofile = true;
        public bool writeMessages = false;

        internal void CreateMenu(DebugMenu parentMenu, string name, int order)
        {
            var menu = new DebugMenu(parentMenu, name, order);
            new DebugMenuToggle(menu, nameof(enable),       () => enable,       value => enable = value,        order: order);
            new DebugMenuToggle(menu, nameof(writeTofile),  () => writeTofile,  value => writeTofile = value,   order: order);
            new DebugMenuToggle(menu, nameof(writeMessages), () => writeMessages, value => writeMessages = value, order: order);
            new DebugMenuFloat (menu, nameof(sampleTime),   () => sampleTime,   value => sampleTime = value,    order: order);
        }
    }
    // Single profilable item
    public class MemoryProfilable
    {
        public MemoryProfilable sibling;
        public string name;

        MemoryProfilable(string name)
        {
            this.name = name;
        }

        public virtual string InspectAllShort() { return string.Empty; }
    }

}