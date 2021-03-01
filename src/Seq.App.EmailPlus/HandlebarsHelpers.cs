using HandlebarsDotNet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace Seq.App.EmailPlus
{
    static class HandlebarsHelpers
    {
        public static void Register()
        {
            Handlebars.RegisterHelper("pretty", PrettyPrintHelper);
            Handlebars.RegisterHelper("if_eq", IfEqHelper);
            Handlebars.RegisterHelper("substring", SubstringHelper);
            Handlebars.RegisterHelper("datetime", DateTimeHelper);
        }

        static void PrettyPrintHelper(TextWriter output, object context, object[] arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.Write(JsonConvert.SerializeObject(FromDynamic(value)));
            else
            {
                var str = value.ToString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    output.WriteSafeString("&nbsp;");
                }
                else
                {
                    output.Write(str);
                }
            }
        }

        static void IfEqHelper(TextWriter output, HelperOptions options, dynamic context, object[] arguments)
        {
            if (arguments?.Length != 2)
            {
                options.Inverse(output, context);
                return;
            }

            var lhs = (arguments[0]?.ToString() ?? "").Trim();
            var rhs = (arguments[1]?.ToString() ?? "").Trim();

            if (lhs.Equals(rhs, StringComparison.Ordinal))
            {
                options.Template(output, context);
            }
            else
            {
                options.Inverse(output, context);
            }
        }

        static object FromDynamic(object o)
        {
            if (o is IEnumerable<KeyValuePair<string, object>> dictionary)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            if (o is IEnumerable<object> enumerable)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        static void SubstringHelper(TextWriter output, object context, object[] arguments)
        {
            //{{ substring value 0 30 }}
            var value = arguments.FirstOrDefault();

            if (value == null)
                return;

            if (arguments.Length < 2)
            {
                // No start or length arguments provided
                output.Write(value);
            }
            else if (arguments.Length < 3)
            {
                // just a start position provided
                int.TryParse(arguments[1].ToString(), out var start);
                if (start > value.ToString().Length)
                {
                    // start of substring after end of string.
                    return;
                }
                output.Write(value.ToString().Substring(start));
            }
            else
            {
                // Start & length provided.
                int.TryParse(arguments[1].ToString(), out var start);
                int.TryParse(arguments[2].ToString(), out var end);

                if (start > value.ToString().Length)
                {
                    // start of substring after end of string.
                    return;
                }
                // ensure the length is still in the string to avoid ArgumentOutOfRangeException
                if (end > value.ToString().Length - start)
                {
                    end = value.ToString().Length - start;
                }

                output.Write(value.ToString().Substring(start, end));
            }
        }

        static void DateTimeHelper(TextWriter output, object context, object[] arguments)
        {
            if (arguments.Length < 1)
                return;

            // Using `DateTimeOffset` avoids ending up with `DateTimeKind.Unspecified` after time zone conversion.
            DateTimeOffset dt;
            if (arguments[0] is DateTimeOffset dto)
                dt = dto;
            else if (arguments[0] is DateTime rdt)
                dt = rdt.Kind == DateTimeKind.Unspecified ? new DateTime(rdt.Ticks, DateTimeKind.Utc) : rdt;
            else
                return;

            string format = null;
            if (arguments.Length >= 2 && arguments[1] is string f)
            {
                format = f;
            }

            if (arguments.Length >= 3 && arguments[2] is string timeZoneId)
            {
                try
                {
                    var tzi = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                    dt = TimeZoneInfo.ConvertTime(dt, tzi);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "A time zone with id {TimeZoneId} was not found; falling back to UTC");
                }
            }
            
            if (dt.Offset == TimeSpan.Zero)
                // Use the idiomatic trailing `Z` formatting for ISO-8601 in UTC.
                output.Write(dt.UtcDateTime.ToString(format));
            else
                output.Write(dt.ToString(format));
        }
    }
}
