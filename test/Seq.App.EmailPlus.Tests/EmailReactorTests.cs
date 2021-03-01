using System;
using System.Collections.Generic;
using System.Linq;
using Seq.App.EmailPlus.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Xunit;

namespace Seq.App.EmailPlus.Tests
{
    public class EmailReactorTests
    {
        static EmailReactorTests()
        {
            // Ensure the handlebars helpers are registered.
            GC.KeepAlive(new EmailApp());
        }

        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{$Level}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("See {{What}}");
            var data = Some.LogEvent(new Dictionary<string, object> { { "What", 10 } });
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("See 10", result);
        }

        [Fact]
        public void NoPropertiesAreRequiredOnASourceEvent()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("No properties");
            var id = Some.EventId();
            var timestamp = Some.UtcTimestamp();
            var data = new Event<LogEventData>(id, Some.EventType(), timestamp, new LogEventData
            {
                Exception = null,
                Id = id,
                Level = LogEventLevel.Fatal,
                LocalTimestamp = new DateTimeOffset(timestamp),
                MessageTemplate = "Some text",
                RenderedMessage = "Some text",
                Properties = null
            });
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("No properties", result);
        }

        [Fact]
        public void IfEqHelperDetectsEquality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Fatal\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("True", result);
        }

        [Fact]
        public void IfEqHelperDetectsInequality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Warning\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("", result);
        }

        [Fact]
        public void TrimStringHelper0Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("", result);
        }

        [Fact]
        public void TrimStringHelper1Arg()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("Fatal", result);
        }

        [Fact]
        public void TrimStringHelper2Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("tal", result);
        }

        [Fact]
        public void TrimStringHelper3Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2 1}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("t", result);
        }
        
        [Fact]
        public void ToAddressesAreTemplated()
        {
            var mail = new CollectingMailGateway();
            var reactor = new EmailApp(mail)
            {
                From = "from@example.com",
                To = "{{Name}}@example.com",
                Host = "example.com"
            };

            reactor.Attach(new TestAppHost());

            var data = Some.LogEvent(new Dictionary<string, object> { { "Name", "test" } });
            reactor.On(data);

            var sent = mail.Sent.Single();
            Assert.Equal("test@example.com", sent.Message.To.ToString());
        }

        [Fact]
        public void DateTimeHelperAppliesFormatting()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{datetime When 'R'}}");
            var data = Some.LogEvent(new Dictionary<string, object>{["When"] = new DateTime(2021, 3, 1, 17, 30, 11, DateTimeKind.Utc)});
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("Mon, 01 Mar 2021 17:30:11 GMT", result);
        }
        
        [Fact]
        public void DateTimeHelperSwitchesTimeZone()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{datetime When 'o' 'Vladivostok Standard Time'}}");
            var data = Some.LogEvent(new Dictionary<string, object>{["When"] = new DateTime(2021, 3, 1, 17, 30, 11, DateTimeKind.Utc)});
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("2021-03-02T03:30:11.0000000+10:00", result);
        }   
        
        [Fact]
        public void UtcFormatsWithZNotation()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{datetime When 'o' 'Coordinated Universal Time'}}");
            var data = Some.LogEvent(new Dictionary<string, object>{["When"] = new DateTime(2021, 3, 1, 17, 30, 11, DateTimeKind.Utc)});
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("2021-03-01T17:30:11.0000000Z", result);
        }
        
        [Fact]
        public void DateTimeHelperRecognizesDefaultUsedInBodyTemplate()
        {
            // `G` is dependent on the server's current culture; maintained for backwards-compatibility
            var template = HandlebarsDotNet.Handlebars.Compile("{{datetime When 'G' 'Coordinated Universal Time'}}");
            var data = Some.LogEvent(new Dictionary<string, object>{["When"] = new DateTime(2021, 3, 1, 17, 30, 11, DateTimeKind.Utc)});
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Contains("2021", result);
        }
    }
}
