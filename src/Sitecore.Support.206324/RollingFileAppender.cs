namespace Sitecore.Support.log4net.Appender
{
  using global::log4net.Appender;
  using global::log4net.helpers;
  using global::log4net.spi;
  using System;
  using System.Collections;
  using System.Globalization;
  using System.IO;

  public class RollingFileAppender : SitecoreFileAppender
  {
    private int m_countDirection = -1;
    internal int m_curSizeRollBackups;
    private string m_datePattern = ".yyyy-MM-dd";
    private IDateTime m_dateTime = new DefaultDateTime();
    private long m_maxFileSize = 0xa00000L;
    internal int m_maxSizeRollBackups;
    private DateTime m_nextCheck = DateTime.MaxValue;
    private DateTime m_now;
    private bool m_rollDate = true;
    private RollingMode m_rollingStyle = RollingMode.Composite;
    private RollPoint m_rollPoint;
    private bool m_rollSize = true;
    private string m_scheduledFilename;
    private bool m_staticLogFileName = true;

    public override void ActivateOptions()
    {
      if (this.m_rollDate && (this.m_datePattern != null))
      {
        this.m_now = this.m_dateTime.Now;
        this.m_rollPoint = this.ComputeCheckPeriod();
        this.m_nextCheck = this.NextCheckDate(this.m_now);
      }
      else if (this.m_rollDate)
      {
        this.ErrorHandler.Error("Either DatePattern or rollingStyle options are not set for [" + base.Name + "].");
      }
      if ((this.m_rollDate && (this.File != null)) && (this.m_scheduledFilename == null))
      {
        this.m_scheduledFilename = this.File + this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
      }
      if (this.m_scheduledFilename == null)
      {
        this.m_scheduledFilename = base.m_originalFileName;
      }
      this.ExistingInit();
      base.ActivateOptions();
    }

    protected override void Append(LoggingEvent loggingEvent)
    {
      if (this.m_rollDate)
      {
        DateTime now = this.m_dateTime.Now;
        if (now >= this.m_nextCheck)
        {
          this.m_now = now;
          this.m_nextCheck = this.NextCheckDate(this.m_now);
          this.RollOverTime();
        }
      }
      if ((this.m_rollSize && (this.File != null)) && (((CountingQuietTextWriter)base.m_qtw).Count >= this.m_maxFileSize))
      {
        this.RollOverSize();
      }
      base.Append(loggingEvent);
    }

    private RollPoint ComputeCheckPeriod()
    {
      if (this.m_datePattern != null)
      {
        DateTime currentDateTime = DateTime.Parse("1970-01-01 00:00:00Z", DateTimeFormatInfo.InvariantInfo);
        string str = currentDateTime.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
        for (int i = 0; i <= 5; i++)
        {
          string str2 = this.NextCheckDate(currentDateTime, (RollPoint)i).ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
          LogLog.Debug(string.Concat(new object[] { "RollingFileAppender: Type = [", i, "], r0 = [", str, "], r1 = [", str2, "]" }));
          if (((str != null) && (str2 != null)) && !str.Equals(str2))
          {
            return (RollPoint)i;
          }
        }
      }
      return RollPoint.TopOfTrouble;
    }

    protected void DeleteFile(string fileName)
    {
      FileInfo info = new FileInfo(fileName);
      if (info.Exists)
      {
        try
        {
          info.Delete();
          LogLog.Debug("RollingFileAppender: Deleted file [" + fileName + "]");
        }
        catch (Exception exception)
        {
          this.ErrorHandler.Error("Exception while deleting file [" + fileName + "]", exception, ErrorCodes.GenericFailure);
        }
      }
    }

    private void DetermineCurSizeRollBackups()
    {
      this.m_curSizeRollBackups = 0;
      string fileName = null;
      if (this.m_staticLogFileName || !this.m_rollDate)
      {
        fileName = base.m_originalFileName;
      }
      else
      {
        fileName = this.m_scheduledFilename;
      }
      FileInfo info = new FileInfo(fileName);
      if (info != null)
      {
        ArrayList existingFiles = GetExistingFiles(info.FullName);
        this.InitializeRollBackups(new FileInfo(base.m_originalFileName).Name, existingFiles);
      }
      LogLog.Debug("RollingFileAppender: curSizeRollBackups starts at [" + this.m_curSizeRollBackups + "]");
    }

    protected void ExistingInit()
    {
      this.DetermineCurSizeRollBackups();
      this.RollOverIfDateBoundaryCrossing();
    }

    internal static ArrayList GetExistingFiles(string baseFilePath)
    {
      ArrayList list = new ArrayList();
      FileInfo info = new FileInfo(baseFilePath);
      DirectoryInfo directory = info.Directory;
      LogLog.Debug("RollingFileAppender: Searching for existing files in [" + directory + "]");
      if (directory.Exists)
      {
        string name = info.Name;
        FileInfo[] files = directory.GetFiles(GetWildcardPatternForFile(name));
        if (files == null)
        {
          return list;
        }
        for (int i = 0; i < files.Length; i++)
        {
          string str2 = files[i].Name;
          if (str2.StartsWith(name))
          {
            list.Add(str2);
          }
        }
      }
      return list;
    }

    private static string GetWildcardPatternForFile(string baseFileName)
    {
      return (baseFileName + "*");
    }

    private void InitializeFromOneFile(string baseFile, string curFileName)
    {
      if (curFileName.StartsWith(baseFile) && !curFileName.Equals(baseFile))
      {
        int num = curFileName.LastIndexOf(".");
        if (-1 != num)
        {
          if (this.m_staticLogFileName)
          {
            int num2 = curFileName.Length - num;
            if ((baseFile.Length + num2) != curFileName.Length)
            {
              return;
            }
          }
          if (this.m_rollDate && !curFileName.StartsWith(baseFile + this.m_dateTime.Now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo)))
          {
            LogLog.Debug("RollingFileAppender: Ignoring file [" + curFileName + "] because it is from a different date period");
          }
          else
          {
            try
            {
              int num3 = int.Parse(curFileName.Substring(num + 1), NumberFormatInfo.InvariantInfo);
              if (num3 > this.m_curSizeRollBackups)
              {
                if (this.m_maxSizeRollBackups != 0)
                {
                  if (-1 == this.m_maxSizeRollBackups)
                  {
                    this.m_curSizeRollBackups = num3;
                  }
                  else if (this.m_countDirection > 0)
                  {
                    this.m_curSizeRollBackups = num3;
                  }
                  else if (num3 <= this.m_maxSizeRollBackups)
                  {
                    this.m_curSizeRollBackups = num3;
                  }
                }
                LogLog.Debug(string.Concat(new object[] { "RollingFileAppender: File name [", curFileName, "] moves current count to [", this.m_curSizeRollBackups, "]" }));
              }
            }
            catch (Exception)
            {
              LogLog.Debug("RollingFileAppender: Encountered a backup file not ending in .x [" + curFileName + "]");
            }
          }
        }
      }
    }

    internal void InitializeRollBackups(string baseFile, ArrayList arrayFiles)
    {
      if (arrayFiles != null)
      {
        string str = baseFile.ToLower(CultureInfo.InvariantCulture);
        foreach (string str2 in arrayFiles)
        {
          this.InitializeFromOneFile(str, str2.ToLower(CultureInfo.InvariantCulture));
        }
      }
    }

    protected DateTime NextCheckDate(DateTime currentDateTime)
    {
      return this.NextCheckDate(currentDateTime, this.m_rollPoint);
    }

    protected DateTime NextCheckDate(DateTime currentDateTime, RollPoint rollPoint)
    {
      DateTime time = currentDateTime;
      switch (rollPoint)
      {
        case RollPoint.TopOfMinute:
          time = time.AddMilliseconds((double)-time.Millisecond);
          return time.AddSeconds((double)-time.Second).AddMinutes(1.0);

        case RollPoint.TopOfHour:
          time = time.AddMilliseconds((double)-time.Millisecond);
          time = time.AddSeconds((double)-time.Second);
          return time.AddMinutes((double)-time.Minute).AddHours(1.0);

        case RollPoint.HalfDay:
          time = time.AddMilliseconds((double)-time.Millisecond);
          time = time.AddSeconds((double)-time.Second);
          time = time.AddMinutes((double)-time.Minute);
          if (time.Hour >= 12)
          {
            return time.AddHours((double)-time.Hour).AddDays(1.0);
          }
          return time.AddHours((double)(12 - time.Hour));

        case RollPoint.TopOfDay:
          time = time.AddMilliseconds((double)-time.Millisecond);
          time = time.AddSeconds((double)-time.Second);
          time = time.AddMinutes((double)-time.Minute);
          return time.AddHours((double)-time.Hour).AddDays(1.0);

        case RollPoint.TopOfWeek:
          time = time.AddMilliseconds((double)-time.Millisecond);
          time = time.AddSeconds((double)-time.Second);
          time = time.AddMinutes((double)-time.Minute);
          time = time.AddHours((double)-time.Hour);
          return time.AddDays((double)(7 - time.DayOfWeek));

        case RollPoint.TopOfMonth:
          {
            time = time.AddMilliseconds((double)-time.Millisecond);
            time = time.AddSeconds((double)-time.Second);
            time = time.AddMinutes((double)-time.Minute);
            time = time.AddHours((double)-time.Hour);
            int introduced1 = DateTime.DaysInMonth(time.Year, time.Month);
            return time.AddDays((double)(introduced1 - time.Day));
          }
      }
      return time;
    }

    protected override void OpenFile(string fileName, bool append)
    {
      RollingFileAppender appender = this;
      lock (appender)
      {
        fileName = base.ProcessFileName();
        if (!this.m_staticLogFileName)
        {
          this.m_scheduledFilename = fileName;
          if (this.m_rollDate)
          {
            this.m_scheduledFilename = fileName = fileName + this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
          }
          if (this.m_countDirection > 0)
          {
            int num2 = this.m_curSizeRollBackups + 1;
            this.m_curSizeRollBackups = num2;
            this.m_scheduledFilename = fileName = fileName + "." + num2;
          }
        }
        long length = 0L;
        if (append)
        {
          FileInfo info = new FileInfo(fileName);
          if (info.Exists)
          {
            length = info.Length;
          }
        }
        base.OpenFile(fileName, append);
        ((CountingQuietTextWriter)base.m_qtw).Count = length;
      }
    }

    protected void RollFile(string fromFile, string toFile)
    {
      FileInfo info = new FileInfo(toFile);
      if (info.Exists)
      {
        LogLog.Debug("RollingFileAppender: Deleting existing target file [" + info + "]");
        info.Delete();
      }
      FileInfo info2 = new FileInfo(fromFile);
      if (info2.Exists)
      {
        try
        {
          info2.MoveTo(toFile);
          LogLog.Debug("RollingFileAppender: Moved [" + fromFile + "] -> [" + toFile + "]");
        }
        catch (Exception exception)
        {
          string[] textArray2 = new string[] { "Exception while rolling file [", fromFile, "] -> [", toFile, "]" };
          this.ErrorHandler.Error(string.Concat(textArray2), exception, ErrorCodes.GenericFailure);
        }
      }
      else
      {
        LogLog.Warn("RollingFileAppender: Cannot RollFile [" + fromFile + "] -> [" + toFile + "]. Source does not exist");
      }
    }

    private void RollOverIfDateBoundaryCrossing()
    {
      if (this.m_staticLogFileName && this.m_rollDate)
      {
        FileInfo info = new FileInfo(base.m_originalFileName);
        if (info.Exists)
        {
          DateTime lastWriteTime = info.LastWriteTime;
          LogLog.Debug("RollingFileAppender: [" + lastWriteTime.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo) + "] vs. [" + this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo) + "]");
          if (!lastWriteTime.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo).Equals(this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo)))
          {
            this.m_scheduledFilename = base.m_originalFileName + lastWriteTime.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
            LogLog.Debug("RollingFileAppender: Initial roll over to [" + this.m_scheduledFilename + "]");
            this.RollOverTime();
            LogLog.Debug("RollingFileAppender: curSizeRollBackups after rollOver at [" + this.m_curSizeRollBackups + "]");
          }
        }
      }
    }

    protected void RollOverSize()
    {
      base.CloseFile();
      LogLog.Debug("RollingFileAppender: rolling over count [" + ((CountingQuietTextWriter)base.m_qtw).Count + "]");
      LogLog.Debug("RollingFileAppender: maxSizeRollBackups [" + this.m_maxSizeRollBackups + "]");
      LogLog.Debug("RollingFileAppender: curSizeRollBackups [" + this.m_curSizeRollBackups + "]");
      LogLog.Debug("RollingFileAppender: countDirection [" + this.m_countDirection + "]");
      if (this.m_maxSizeRollBackups != 0)
      {
        if (this.m_countDirection < 0)
        {
          if (this.m_curSizeRollBackups == this.m_maxSizeRollBackups)
          {
            this.DeleteFile(this.File + "." + this.m_maxSizeRollBackups);
            this.m_curSizeRollBackups--;
          }
          for (int i = this.m_curSizeRollBackups; i >= 1; i--)
          {
            this.RollFile(this.File + "." + i, this.File + "." + (i + 1));
          }
          this.m_curSizeRollBackups++;
          this.RollFile(this.File, this.File + ".1");
        }
        else
        {
          if ((this.m_curSizeRollBackups >= this.m_maxSizeRollBackups) && (this.m_maxSizeRollBackups > 0))
          {
            this.DeleteFile(this.File + "." + ((this.m_curSizeRollBackups - this.m_maxSizeRollBackups) + 1));
          }
          if (this.m_staticLogFileName)
          {
            this.m_curSizeRollBackups++;
            this.RollFile(this.File, this.File + "." + this.m_curSizeRollBackups);
          }
        }
      }
      try
      {
        this.OpenFile(base.m_originalFileName, false);
      }
      catch (Exception exception)
      {
        this.ErrorHandler.Error("OpenFile [" + base.m_originalFileName + "] call failed.", exception);
      }
    }

    protected void RollOverTime()
    {
      if (this.m_staticLogFileName)
      {
        if (this.m_datePattern == null)
        {
          this.ErrorHandler.Error("Missing DatePattern option in rollOver().");
          return;
        }
        string str = this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
        if (this.m_scheduledFilename.Equals(this.File + str))
        {
          string[] textArray1 = new string[] { "Compare ", this.m_scheduledFilename, " : ", this.File, str };
          this.ErrorHandler.Error(string.Concat(textArray1));
          return;
        }
        base.CloseFile();
        for (int i = 1; i <= this.m_curSizeRollBackups; i++)
        {
          string fromFile = this.File + "." + i;
          string toFile = this.m_scheduledFilename + "." + i;
          this.RollFile(fromFile, toFile);
        }
        this.RollFile(this.File, this.m_scheduledFilename);
      }
      try
      {
        this.m_curSizeRollBackups = 0;
        this.m_scheduledFilename = this.File + this.m_now.ToString(this.m_datePattern, DateTimeFormatInfo.InvariantInfo);
        this.OpenFile(base.m_originalFileName, false);
      }
      catch (Exception exception)
      {
        this.ErrorHandler.Error("setFile(" + this.File + ", false) call failed.", exception, ErrorCodes.FileOpenFailure);
      }
    }

    protected override void SetQWForFiles(TextWriter writer)
    {
      base.m_qtw = new CountingQuietTextWriter(writer, this.ErrorHandler);
    }

    public int CountDirection
    {
      get
      {
        return this.m_countDirection;
      }
      set
      {
        this.m_countDirection = value;
      }
    }

    public string DatePattern
    {
      get
      {
        return this.m_datePattern;
      }
      set
      {
        this.m_datePattern = value;
      }
    }

    public long MaxFileSize
    {
      get
      {
        return this.m_maxFileSize;
      }
      set
      {
        this.m_maxFileSize = value;
      }
    }

    public string MaximumFileSize
    {
      get
      {
        return this.m_maxFileSize.ToString(NumberFormatInfo.InvariantInfo);
      }
      set
      {
        this.m_maxFileSize = OptionConverter.ToFileSize(value, this.m_maxFileSize + 1L);
      }
    }

    public int MaxSizeRollBackups
    {
      get
      {
        return this.m_maxSizeRollBackups;
      }
      set
      {
        this.m_maxSizeRollBackups = value;
      }
    }

    public RollingMode RollingStyle
    {
      get
      {
        return this.m_rollingStyle;
      }
      set
      {
        this.m_rollingStyle = value;
        switch (this.m_rollingStyle)
        {
          case RollingMode.Size:
            this.m_rollDate = false;
            this.m_rollSize = true;
            return;

          case RollingMode.Date:
            this.m_rollDate = true;
            this.m_rollSize = false;
            return;

          case RollingMode.Composite:
            this.m_rollDate = true;
            this.m_rollSize = true;
            return;
        }
      }
    }

    public bool StaticLogFileName
    {
      get
      {
        return this.m_staticLogFileName;
      }
      set
      {
        this.m_staticLogFileName = value;
      }
    }

    private class DefaultDateTime : RollingFileAppender.IDateTime
    {
      public DateTime Now
      {
        get
        {
          return DateTime.Now;
        }
      }
    }

    public interface IDateTime
    {
      DateTime Now { get; }
    }

    public enum RollingMode
    {
      Composite = 3,
      Date = 2,
      Size = 1
    }

    protected enum RollPoint
    {
      HalfDay = 2,
      TopOfDay = 3,
      TopOfHour = 1,
      TopOfMinute = 0,
      TopOfMonth = 5,
      TopOfTrouble = -1,
      TopOfWeek = 4
    }
  }
}
