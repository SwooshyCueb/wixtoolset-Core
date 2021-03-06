// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.WindowsInstaller.Bind
{
    using System;
    using System.IO;
    using WixToolset.Data;
    using WixToolset.Data.WindowsInstaller;
    using WixToolset.Data.WindowsInstaller.Rows;
    using WixToolset.Extensibility.Services;

    internal class UpdateControlTextCommand
    {
        public IMessaging Messaging { private get; set; }

        public Table BBControlTable { private get; set; }

        public Table WixBBControlTable { private get; set; }

        public Table ControlTable { private get; set; }

        public Table WixControlTable { private get; set; }

        public void Execute()
        {
            if (null != this.WixBBControlTable)
            {
                RowDictionary<BBControlRow> bbControlRows = new RowDictionary<BBControlRow>(this.BBControlTable);
                foreach (Row wixRow in this.WixBBControlTable.Rows)
                {
                    BBControlRow bbControlRow = bbControlRows.Get(wixRow.GetPrimaryKey());
                    bbControlRow.Text = this.ReadTextFile(bbControlRow.SourceLineNumbers, wixRow.FieldAsString(2));
                }
            }

            if (null != this.WixControlTable)
            {
                RowDictionary<ControlRow> controlRows = new RowDictionary<ControlRow>(this.ControlTable);
                foreach (Row wixRow in this.WixControlTable.Rows)
                {
                    ControlRow controlRow = controlRows.Get(wixRow.GetPrimaryKey());
                    controlRow.Text = this.ReadTextFile(controlRow.SourceLineNumbers, wixRow.FieldAsString(2));
                }
            }
        }

        /// <summary>
        /// Reads a text file and returns the contents.
        /// </summary>
        /// <param name="sourceLineNumbers">Source line numbers for row from source.</param>
        /// <param name="source">Source path to file to read.</param>
        /// <returns>Text string read from file.</returns>
        private string ReadTextFile(SourceLineNumber sourceLineNumbers, string source)
        {
            string text = null;

            try
            {
                using (StreamReader reader = new StreamReader(source))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (DirectoryNotFoundException e)
            {
                this.Messaging.Write(ErrorMessages.BinderFileManagerMissingFile(sourceLineNumbers, e.Message));
            }
            catch (FileNotFoundException e)
            {
                this.Messaging.Write(ErrorMessages.BinderFileManagerMissingFile(sourceLineNumbers, e.Message));
            }
            catch (IOException e)
            {
                this.Messaging.Write(ErrorMessages.BinderFileManagerMissingFile(sourceLineNumbers, e.Message));
            }
            catch (NotSupportedException)
            {
                this.Messaging.Write(ErrorMessages.FileNotFound(sourceLineNumbers, source));
            }

            return text;
        }
    }
}
