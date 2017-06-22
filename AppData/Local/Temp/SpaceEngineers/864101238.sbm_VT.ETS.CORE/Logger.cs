/*
 * VerdanTech Extensible Terminal System Core
 * Copyright © 2017, VerdanTech
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Sandbox.ModAPI;
using Sandbox.Common;

namespace VT.ETS.CORE
{
    /// <summary>
    /// Used to write a composite log-file serving as an event-based debugging utility.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// The text-writer used to write content to the composite file.
        /// </summary>
        private static System.IO.TextWriter m_writer;
        /// <summary>
        /// The string-builder used to aggregate content for the composite file.
        /// </summary>
        private static StringBuilder m_cache = new StringBuilder();
        /// <summary>
        /// The set of logs represented in the composite file.
        /// </summary>
        private static HashSet<string> runningLogs = new HashSet<string>();
        /// <summary>
        /// The value reflecting whether first-run setup to create needed files has been done.
        /// </summary>
        private static bool isInitialized = false;
        /// <summary>
        /// The value reflecting whether the logger has been terminated or not.
        /// </summary>
        private static bool isTerminated = false;
        /// <summary>
        /// The name of the log associated with the current instance.
        /// </summary>
        private string entryName = "";
        /// <summary>
        /// Initializes the stream, if necessary, and then adds the log into the composite file.
        /// </summary>
        /// <param name="logName">The selected name for the given log.</param>
        public Logger(string logName)
        {
            if (MyAPIGateway.Utilities == null || isTerminated)
            {
                return;
            }
            // Set up the log-file.
            Initialize();
            // Add the specified log to our running list.
            AddLog(logName);
        }
        /// <summary>
        /// Generates a composite logfile, incrementing based on the existence of others in local storage.
        /// </summary>
        private void Initialize()
        {
            // We don't need to initialize if we've already got a log-file running.
            if (!isInitialized)
            {
                // Prepare session-end escape.
                MyAPIGateway.Entities.OnCloseAll += Shutdown;
                // We won't need to run this after this time.
                isInitialized = true;
                // The name of the file gets initialized as specified.
                string fileName = GetUnusedFileNameVariant();
                // Now open a writer for the log-file with the acquired file name.
                m_writer = GetWriterForFilename(fileName);
            }
        }
        /// <summary>
        /// Iterates over the number of logs in storage, choosing the next name in sequence.
        /// </summary>
        /// <returns>A suffixed variation on the Log.log indicating the number.</returns>
        private string GetUnusedFileNameVariant()
        {
            // We need to track how many log-files we've got in the directory.
            int copy = 0;
            // The name of the file gets initialized as specified.
            string nextName = "Log_0.log";
            // Now we keep incrementing the log-file copy number until we find an available number.
            while (FileNameTaken(nextName))
            {
                // Increment to the next potential copy number...
                copy++;
                // and rename the file to match that next potential copy.
                nextName = "Log_" + copy + ".log";
            }
            // return the final file name version.
            return nextName;
        }
        /// <summary>
        /// Checks the argument to see if a file of the same name exists.
        /// </summary>
        /// <param name="fileName">The filename to be checked for existence.</param>
        /// <returns>True if the filename belongs to an existing file.</returns>
        private bool FileNameTaken(string fileName)
        {
            // Determine whether the current file name is taken in the local storage.
            return MyAPIGateway.Utilities.FileExistsInLocalStorage(fileName, (Type)typeof(Logger));
        }
        /// <summary>
        /// Simply tells the API-gateway to create a file in local storage by the given name.
        /// </summary>
        /// <param name="fileName"> The name of the file to be written.</param>
        /// <returns>The TextWriter used to set the content of the specified file.</returns>
        private TextWriter GetWriterForFilename(string fileName)
        {
            return MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, (Type)typeof(Logger));
        }
        /// <summary>
        /// Handles the inclusion of the defined log into the given composite file.
        /// </summary>
        /// <param name="logName">The desired name for the given log, subject to change based on existing instance naming.</param>
        private void AddLog(string logName)
        {
            // We don't allow for shared names between running logs, so we check the copies of each.
            int copy = 0;
            // We assume the provided log-name provides the content, so we append the copy-number to it.
            entryName = logName + "_0";
            // So long as we're on an in-use name, keep looping.
            while (runningLogs.Contains(entryName))
            {
                // If the given name is taken, increment the copy-count.
                copy++;
                // Then alter the entry name to the new copy-suffix.
                entryName = logName + "_" + copy.ToString();
            }
            // Add the unused name to our running log set.
            runningLogs.Add(entryName);
        }
        /// <summary>
        /// Create an entry prepended by the instance-name and time-stamp with the given content.
        /// </summary>
        /// <param name="text">The content of the line to be written.</param>
        public void WriteLine(string text)
        {
            if (isTerminated)
                return;
            // We check for waiting content in the cache...
            if (m_cache.Length > 0)
            {
                // and write any of it to the file.
                m_writer.WriteLine(m_cache);
            }
            // Now we erase the cache's contents...
            m_cache.Clear();
            // We add the entry name and the timestamp in brackets and flanked by tabs.
            m_cache.Append("{" + entryName + "}" + DateTime.Now.ToString("\t[HH:mm:ss.ffffff]\t"));
            // Then we write the text that was specified.
            m_writer.WriteLine(m_cache.Append(text));
            // Then we empty the writer's buffer out.
            m_writer.Flush();
            // Now we erase the cache's contents.
            m_cache.Clear();
        }
        /// <summary>
        /// Deactivate the given logger instance, and if no others are active, close the stream.
        /// </summary>
        public void CloseLog()
        {
            // We're closing, so that's one less log running.
            runningLogs.Remove(entryName);
        }
        /// <summary>
        /// Finalize the logging stream for termination.
        /// </summary>
        private void Shutdown()
        {
            // We need to stop listening for the shutdown signal.
            MyAPIGateway.Entities.OnCloseAll -= Shutdown;
            // We need to check if there is unwritten content in the cache.
            if (m_cache.Length > 0)
            {
                // Write the remaining content.
                m_writer.WriteLine(m_cache);
            }
            // Empty the writer's buffer out.
            m_writer.Flush();
            // Now shut the writer down.
            m_writer.Close();
            // Mark the logger as unavailable.
            isTerminated = true;
        }
    }
}