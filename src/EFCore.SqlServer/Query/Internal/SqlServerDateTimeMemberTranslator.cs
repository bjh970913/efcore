// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query.Internal
{
    public class SqlServerDateTimeMemberTranslator : IMemberTranslator
    {
        private static readonly Dictionary<string, string> _datePartMapping
            = new Dictionary<string, string>
            {
                { nameof(DateTime.Year), "year" },
                { nameof(DateTime.Month), "month" },
                { nameof(DateTime.DayOfYear), "dayofyear" },
                { nameof(DateTime.Day), "day" },
                { nameof(DateTime.Hour), "hour" },
                { nameof(DateTime.Minute), "minute" },
                { nameof(DateTime.Second), "second" },
                { nameof(DateTime.Millisecond), "millisecond" }
            };

        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public SqlServerDateTimeMemberTranslator(ISqlExpressionFactory sqlExpressionFactory)
        {
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        public virtual SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType)
        {
            var declaringType = member.DeclaringType;

            if (declaringType == typeof(DateTime)
                || declaringType == typeof(DateTimeOffset))
            {
                var memberName = member.Name;

                if (_datePartMapping.TryGetValue(memberName, out var datePart))
                {
                    return _sqlExpressionFactory.Function(
                        "DATEPART",
                        new[] { _sqlExpressionFactory.Fragment(datePart), instance },
                        returnType);
                }

                switch (memberName)
                {
                    case nameof(DateTime.Date):
                        if (AppContext.TryGetSwitch("Microsoft.EntityFrameworkCore.Issue19052", out var isEnabled) && isEnabled)
                        {
                            return _sqlExpressionFactory.Function(
                                "CONVERT",
                                new[] { _sqlExpressionFactory.Fragment("date"), instance },
                                returnType,
                                instance.TypeMapping);
                        }

                        return _sqlExpressionFactory.Function(
                            "CONVERT",
                            new[] { _sqlExpressionFactory.Fragment("date"), instance },
                            returnType,
                            declaringType == typeof(DateTime)
                                ? instance.TypeMapping
                                : _sqlExpressionFactory.FindMapping(typeof(DateTime)));

                    case nameof(DateTime.TimeOfDay):
                        return _sqlExpressionFactory.Convert(instance, returnType);

                    case nameof(DateTime.Now):
                        return _sqlExpressionFactory.Function(
                            declaringType == typeof(DateTime) ? "GETDATE" : "SYSDATETIMEOFFSET",
                            Array.Empty<SqlExpression>(),
                            returnType);

                    case nameof(DateTime.UtcNow):
                        var serverTranslation = _sqlExpressionFactory.Function(
                            declaringType == typeof(DateTime) ? "GETUTCDATE" : "SYSUTCDATETIME",
                            Array.Empty<SqlExpression>(),
                            returnType);

                        return declaringType == typeof(DateTime)
                            ? (SqlExpression)serverTranslation
                            : _sqlExpressionFactory.Convert(serverTranslation, returnType);

                    case nameof(DateTime.Today):
                        return _sqlExpressionFactory.Function(
                            "CONVERT",
                            new SqlExpression[]
                            {
                                _sqlExpressionFactory.Fragment("date"),
                                _sqlExpressionFactory.Function(
                                    "GETDATE",
                                    Array.Empty<SqlExpression>(),
                                    typeof(DateTime))
                            },
                            returnType);
                }
            }

            return null;
        }
    }
}
