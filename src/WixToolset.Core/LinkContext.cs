﻿// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core
{
    using System;
    using System.Collections.Generic;
    using WixToolset.Data;
    using WixToolset.Extensibility;
    using WixToolset.Extensibility.Data;

    internal class LinkContext : ILinkContext
    {
        internal LinkContext(IServiceProvider serviceProvider)
        {
            this.ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public IEnumerable<ILinkerExtension> Extensions { get; set; }

        public IEnumerable<IExtensionData> ExtensionData { get; set; }

        public OutputType ExpectedOutputType { get; set; }

        public IEnumerable<Intermediate> Intermediates { get; set; }

        public ITupleDefinitionCreator TupleDefinitionCreator { get; set; }
    }
}
