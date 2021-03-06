﻿// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.ExtensibilityServices
{
    using System;
    using System.Linq;
    using WixToolset.Data;
    using WixToolset.Data.WindowsInstaller;
    using WixToolset.Extensibility.Services;

    internal class WindowsInstallerBackendHelper : IWindowsInstallerBackendHelper
    {
        public WindowsInstallerBackendHelper(IServiceProvider serviceProvider)
        {
            this.ServiceProvider = serviceProvider;
        }

        private IServiceProvider ServiceProvider { get; }

        public bool TryAddTupleToOutputMatchingTableDefinitions(IntermediateTuple tuple, Output output, TableDefinition[] tableDefinitions)
        {
            var tableDefinition = tableDefinitions.FirstOrDefault(t => t.Name == tuple.Definition.Name);

            if (tableDefinition == null)
            {
                return false;
            }

            var table = output.EnsureTable(tableDefinition);
            var row = table.CreateRow(tuple.SourceLineNumbers);
            for (var i = 0; i < tuple.Fields.Length; ++i)
            {
                if (i < tableDefinition.Columns.Length)
                {
                    var column = tableDefinition.Columns[i];

                    switch (column.Type)
                    {
                        case ColumnType.Number:
                            row[i] = tuple.AsNumber(i);
                            break;

                        default:
                            row[i] = tuple.AsString(i);
                            break;
                    }
                }
            }

            return true;
        }
    }
}
