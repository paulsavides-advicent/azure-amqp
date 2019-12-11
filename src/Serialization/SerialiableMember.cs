// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Amqp.Serialization
{
    using System;

    sealed class SerialiableMember
    {
        public string Name
        {
            get;
            set;
        }

        public int Order
        {
            get;
            set;
        }

        public bool Mandatory
        {
            get;
            set;
        }

        public Func<object, object> Get
        {
            get;
            set;
        }

        public Action<object, object> Set
        {
            get;
            set;
        }

        public SerializableType Type
        {
            get;
            set;
        }
    }
}
