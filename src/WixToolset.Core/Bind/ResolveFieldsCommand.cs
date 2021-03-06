// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.Bind
{
    using System;
    using System.Collections.Generic;
    using WixToolset.Data;
    using WixToolset.Extensibility;
    using WixToolset.Extensibility.Data;
    using WixToolset.Extensibility.Services;

    /// <summary>
    /// Resolve source fields in the tables included in the output
    /// </summary>
    internal class ResolveFieldsCommand
    {
        public IMessaging Messaging { private get; set; }

        public bool BuildingPatch { private get; set; }

        public IVariableResolver VariableResolver { private get; set; }

        public IEnumerable<BindPath> BindPaths { private get; set; }

        public IEnumerable<IResolverExtension> Extensions { private get; set; }

        public ExtractEmbeddedFiles FilesWithEmbeddedFiles { private get; set; }

        public string IntermediateFolder { private get; set; }

        public Intermediate Intermediate { private get; set; }

        public bool SupportDelayedResolution { private get; set; }

        public IEnumerable<DelayedField> DelayedFields { get; private set; }

        public void Execute()
        {
            List<DelayedField> delayedFields = this.SupportDelayedResolution ? new List<DelayedField>() : null;

            var fileResolver = new FileResolver(this.BindPaths, this.Extensions);

            foreach (var sections in this.Intermediate.Sections)
            {
                foreach (var row in sections.Tuples)
                {
                    foreach (var field in row.Fields)
                    {
                        if (field == null)
                        {
                            continue;
                        }

                        var isDefault = true;

                        // Check to make sure we're in a scenario where we can handle variable resolution.
                        if (null != delayedFields)
                        {
                            // resolve localization and wix variables
                            if (field.Type == IntermediateFieldType.String)
                            {
                                var original = field.AsString();
                                if (!String.IsNullOrEmpty(original))
                                {
                                    var resolution = this.VariableResolver.ResolveVariables(row.SourceLineNumbers, original, false);
                                    if (resolution.UpdatedValue)
                                    {
                                        field.Set(resolution.Value);
                                    }

                                    if (resolution.DelayedResolve)
                                    {
                                        delayedFields.Add(new DelayedField(row, field));
                                    }

                                    isDefault = resolution.IsDefault;
                                }
                            }
                        }

                        // Move to next row if we've hit an error resolving variables.
                        if (this.Messaging.EncounteredError) // TODO: make this error handling more specific to just the failure to resolve variables in this field.
                        {
                            continue;
                        }

                        // Resolve file paths
                        if (field.Type == IntermediateFieldType.Path)
                        {
                            var objectField = field.AsPath();

#if REVISIT_FOR_PATCHING
                            // Skip file resolution if the file is to be deleted.
                            if (RowOperation.Delete == row.Operation)
                            {
                                continue;
                            }
#endif

                            // File is embedded and path to it was not modified above.
                            if (objectField.EmbeddedFileIndex.HasValue && isDefault)
                            {
                                var extractPath = this.FilesWithEmbeddedFiles.AddEmbeddedFileIndex(objectField.BaseUri, objectField.EmbeddedFileIndex.Value, this.IntermediateFolder);

                                // Set the path to the embedded file once where it will be extracted.
                                field.Set(extractPath);
                            }
                            else if (null != objectField.Path) // non-compressed file (or localized value)
                            {
                                try
                                {
                                    if (!this.BuildingPatch) // Normal binding for non-Patch scenario such as link (light.exe)
                                    {
#if REVISIT_FOR_PATCHING
                                        // keep a copy of the un-resolved data for future replay. This will be saved into wixpdb file
                                        if (null == objectField.UnresolvedData)
                                        {
                                            objectField.UnresolvedData = (string)objectField.Data;
                                        }
#endif

                                        // resolve the path to the file
                                        var value = fileResolver.ResolveFile(objectField.Path, row.Definition, row.SourceLineNumbers, BindStage.Normal);
                                        field.Set(value);
                                    }
                                    else if (!fileResolver.RebaseTarget && !fileResolver.RebaseUpdated) // Normal binding for Patch Scenario (normal patch, no re-basing logic)
                                    {
                                        // resolve the path to the file
                                        var value = fileResolver.ResolveFile(objectField.Path, row.Definition, row.SourceLineNumbers, BindStage.Normal);
                                        field.Set(value);
                                    }
#if REVISIT_FOR_PATCHING
                                    else // Re-base binding path scenario caused by pyro.exe -bt -bu
                                    {
                                        // by default, use the resolved Data for file lookup
                                        string filePathToResolve = (string)objectField.Data;

                                        // if -bu is used in pyro command, this condition holds true and the tool
                                        // will use pre-resolved source for new wixpdb file
                                        if (fileResolver.RebaseUpdated)
                                        {
                                            // try to use the unResolved Source if it exists.
                                            // New version of wixpdb file keeps a copy of pre-resolved Source. i.e. !(bindpath.test)\foo.dll
                                            // Old version of winpdb file does not contain this attribute and the value is null.
                                            if (null != objectField.UnresolvedData)
                                            {
                                                filePathToResolve = objectField.UnresolvedData;
                                            }
                                        }

                                        objectField.Data = fileResolver.ResolveFile(filePathToResolve, row.Definition.Name, row.SourceLineNumbers, BindStage.Updated);
                                    }
#endif
                                }
                                catch (WixException e)
                                {
                                    this.Messaging.Write(e.Error);
                                }
                            }

#if REVISIT_FOR_PATCHING
                            if (null != objectField.PreviousData)
                            {
                                objectField.PreviousData = this.BindVariableResolver.ResolveVariables(row.SourceLineNumbers, objectField.PreviousData, false, out isDefault);

                                if (!Messaging.Instance.EncounteredError) // TODO: make this error handling more specific to just the failure to resolve variables in this field.
                                {
                                    // file is compressed in a cabinet (and not modified above)
                                    if (objectField.PreviousEmbeddedFileIndex.HasValue && isDefault)
                                    {
                                        // when loading transforms from disk, PreviousBaseUri may not have been set
                                        if (null == objectField.PreviousBaseUri)
                                        {
                                            objectField.PreviousBaseUri = objectField.BaseUri;
                                        }

                                        string extractPath = this.FilesWithEmbeddedFiles.AddEmbeddedFileIndex(objectField.PreviousBaseUri, objectField.PreviousEmbeddedFileIndex.Value, this.IntermediateFolder);

                                        // set the path to the file once its extracted from the cabinet
                                        objectField.PreviousData = extractPath;
                                    }
                                    else if (null != objectField.PreviousData) // non-compressed file (or localized value)
                                    {
                                        try
                                        {
                                            if (!fileResolver.RebaseTarget && !fileResolver.RebaseUpdated)
                                            {
                                                // resolve the path to the file
                                                objectField.PreviousData = fileResolver.ResolveFile((string)objectField.PreviousData, row.Definition.Name, row.SourceLineNumbers, BindStage.Normal);
                                            }
                                            else
                                            {
                                                if (fileResolver.RebaseTarget)
                                                {
                                                    // if -bt is used, it come here
                                                    // Try to use the original unresolved source from either target build or update build
                                                    // If both target and updated are of old wixpdb, it behaves the same as today, no re-base logic here
                                                    // If target is old version and updated is new version, it uses unresolved path from updated build
                                                    // If both target and updated are of new versions, it uses unresolved path from target build
                                                    if (null != objectField.UnresolvedPreviousData || null != objectField.UnresolvedData)
                                                    {
                                                        objectField.PreviousData = objectField.UnresolvedPreviousData ?? objectField.UnresolvedData;
                                                    }
                                                }

                                                // resolve the path to the file
                                                objectField.PreviousData = fileResolver.ResolveFile((string)objectField.PreviousData, row.Definition.Name, row.SourceLineNumbers, BindStage.Target);

                                            }
                                        }
                                        catch (WixFileNotFoundException)
                                        {
                                            // display the error with source line information
                                            Messaging.Instance.Write(WixErrors.FileNotFound(row.SourceLineNumbers, (string)objectField.PreviousData));
                                        }
                                    }
                                }
                            }
#endif
                        }
                    }
                }
            }

            this.DelayedFields = delayedFields;
        }

#if false
        private string ResolveFile(string source, string type, SourceLineNumber sourceLineNumbers, BindStage bindStage = BindStage.Normal)
        {
            string path = null;
            foreach (var extension in this.Extensions)
            {
                path = extension.ResolveFile(source, type, sourceLineNumbers, bindStage);
                if (null != path)
                {
                    break;
                }
            }

            throw new NotImplementedException(); // need to do default binder stuff

            //if (null == path)
            //{
            //    throw new WixFileNotFoundException(sourceLineNumbers, source, type);
            //}

            //return path;
        }
#endif
    }
}
