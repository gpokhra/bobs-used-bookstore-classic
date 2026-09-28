using NLog;
using NLog.Config;
using NLog.Targets;

namespace Bookstore.Web
{
    public static class LoggingSetup
    {
        public static void ConfigureNLog(string loggingMode)
        {
            if (loggingMode == "aws")
            {
                // NLog.AWS.Logger reads its config from nlog.config / appsettings.json.
                // Calling ReconfigExistingLoggers() ensures any existing loggers pick up
                // the CloudWatch target if it was configured via config file.
                LogManager.ReconfigExistingLoggers();
                return;
            }

            ConfigureLocalLogging();
        }

        private static void ConfigureLocalLogging()
        {
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget("logfile")
            {
                FileName = "${basedir}/logs/${shortdate}.log",
                Layout = "${longdate} ${uppercase:${level}} ${logger} ${message} ${exception:format=tostring}"
            };

            var consoleTarget = new ColoredConsoleTarget("console")
            {
                Layout = "${longdate} ${uppercase:${level}} ${message} ${exception:format=tostring}"
            };

            config.AddTarget(fileTarget);
            config.AddTarget(consoleTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, consoleTarget);

            LogManager.Configuration = config;
        }
    }
}
