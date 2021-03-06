﻿// (C) Copyright 2015 ETAS GmbH (http://www.etas.com/)
// Distributed under the Boost Software License, Version 1.0.
// (See accompanying file LICENSE_1_0.txt or copy at
// http://www.boost.org/LICENSE_1_0.txt)

namespace BoostTestAdapter.Boost.Results.LogEntryTypes
{
    /// <summary>
    /// An 'Info' log entry
    /// </summary>
    public class LogEntryInfo : LogEntry
    {
        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public LogEntryInfo()
        {
        }
        
        #endregion Constructors

        /// <summary>
        /// returns a string with the description of the class
        /// </summary>
        /// <returns>string having the description of the class</returns>
        public override string ToString()
        {
            return "Info";
        }
    }
}