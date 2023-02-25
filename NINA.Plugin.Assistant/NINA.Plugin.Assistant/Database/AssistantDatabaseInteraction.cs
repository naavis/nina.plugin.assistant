﻿using NINA.Core.Utility;
using System;
using System.IO;

namespace Assistant.NINAPlugin.Database {

    public class AssistantDatabaseInteraction {

        static AssistantDatabaseInteraction() {
            DllLoader.LoadDll(Path.Combine("SQLite", "SQLite.Interop.dll"));
        }

        private string connectionString;

        public AssistantDatabaseInteraction()
            : this(string.Format(@"Data Source={0};", Environment.ExpandEnvironmentVariables($@"{AssistantPlugin.PLUGIN_HOME}\assistantdb.sqlite"))) {
        }

        public AssistantDatabaseInteraction(string connectionString) {
            this.connectionString = connectionString;
        }

        public AssistantDatabaseContext GetContext() {
            return new AssistantDatabaseContext(connectionString);
        }
    }

}
