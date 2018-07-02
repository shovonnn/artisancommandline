using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using McMaster.Extensions.CommandLineUtils;

namespace ArtisanCommandLine
{
    /// <summary>
    /// Adds extension method to <see cref="CommandLineApplication"/> Instance
    /// </summary>
    public static class CommandLineApplicationConventionExtension
    {
        private const string HandleMethodPrefix = "Handle";
        /// <summary>
        /// Reads properties and methods using reflection apis and registers options/arguments or subcommand
        /// </summary>
        /// <param name="app">Main command instance</param>
        /// <param name="name">If specified this command is registered as subcommand of the main command</param>
        /// <typeparam name="T">Type of the command to register</typeparam>
        /// <exception cref="Exception">Exception when class has multiple handle method with same name</exception>
        public static void BindConvention<T>(this CommandLineApplication app, string name = null) where T : class
        {
            CommandLineApplication command;
            if (name != null)
                command = app.Command(name, (obj) => { });
            else command = app;
            var type = typeof(T);
            var props = type.GetProperties();
            var desc = type.GetCustomAttribute<ConventionDescriptionAttribute>();
            if (desc != null)
            {
                command.Description = desc.Text;
            }
            foreach (var prop in props)
            {
                if (prop.GetCustomAttribute<ConventionIgnoreAttribute>() == null)
                {
                    _RegisterOption(command, prop.PropertyType, _PascalToKebab(prop.Name), prop.GetCustomAttribute<ConventionDescriptionAttribute>()?.Text ?? "", prop.GetCustomAttribute<ConventionIsRequiredAttribute>() != null);
                }
            }
            var methods = type.GetMethods().Where(method => method.Name.StartsWith(HandleMethodPrefix, StringComparison.InvariantCulture)).ToArray();

            if (_HasDuplicateHandleMethod(methods))
                throw new Exception("There cannot be multiple handle method with same name");

            foreach (var method in methods)
            {
                var subCommandName = _PascalToKebab(method.Name.Substring(HandleMethodPrefix.Length));
                if (String.IsNullOrEmpty(subCommandName))
                {
                    _ConfigureInvoker(command, type, method);
                }
                else
                {
                    var comma = command.Command(subCommandName, subCommand => _ConfigureInvoker(subCommand, type, method));
                    if (method.GetCustomAttribute<ConventionDescriptionAttribute>() != null)
                    {
                        comma.Description = method.GetCustomAttribute<ConventionDescriptionAttribute>().Text;
                    }
                }
            }
        }
        private static void _ConfigureInvoker(CommandLineApplication command, Type type, MethodInfo methodInfo)
        {
            var helpOpt = command.HelpOption();
            var parameters = methodInfo.GetParameters();
            foreach (var param in parameters)
            {
                if (param.GetCustomAttribute<ConventionOptionAttribute>() != null)
                {
                    _RegisterOption(command, param.ParameterType, _PascalToKebab(param.Name), param.GetCustomAttribute<ConventionDescriptionAttribute>()?.Text ?? "", param.GetCustomAttribute<ConventionIsRequiredAttribute>() != null);
                }
                else if (_IsExpectingSingleValue(param.ParameterType) || _IsExpectingMultiValue(param.ParameterType))
                {
                    var desc = param.GetCustomAttribute<ConventionDescriptionAttribute>();
                    var description = desc?.Text ?? "";
                    command.Argument(param.Name, description, _IsExpectingMultiValue(param.ParameterType));
                }
            }
            command.OnExecute(() =>
            {
                if (helpOpt.HasValue()) return 0;
                CancellationTokenSource cts = null;
                var obj = Activator.CreateInstance(type);
                var alloptions = command.GetOptions();
                foreach (var prop in type.GetProperties())
                {
                    var option = alloptions.FirstOrDefault(opt => opt.LongName == _PascalToKebab(prop.Name));
                    if (option != null) prop.SetValue(obj, _ExtractValueFromOption(prop.PropertyType, option));
                }
                if (type.GetMethod("Initialize") != null)
                {
                    type.GetMethod("Initialize").Invoke(obj, null);
                }

                var allArgs = command.Arguments;
                var handlerParams = new List<object>();
                foreach (var param in methodInfo.GetParameters())
                {
                    var arg = allArgs.FirstOrDefault(a => a.Name == param.Name);
                    var option = alloptions.FirstOrDefault(opt => opt.LongName == _PascalToKebab(param.Name));
                    object valueToAdd = null;

                    if (arg != null)
                    {
                        if (arg.MultipleValues) valueToAdd = _ParseMultiValue(param.ParameterType, arg.Values);
                        else if (param.HasDefaultValue && arg.Value == null) valueToAdd = param.DefaultValue;
                        else valueToAdd = _ParseSingleValue(param.ParameterType, arg.Value);
                    }
                    else if (option != null)
                    {
                        if (param.HasDefaultValue && option.HasValue() == false) valueToAdd = param.DefaultValue;
                        else valueToAdd = _ExtractValueFromOption(param.ParameterType, option);
                    }
                    else if (param.ParameterType == typeof(CancellationToken))
                    {
                        if (cts == null) cts = new CancellationTokenSource();
                        valueToAdd = cts.Token;
                    }
                    else
                    {
                        if (type.GetMethod("ResolveType") != null)
                        {
                            valueToAdd = type.GetMethod("ResolveType").Invoke(obj, new[] { param.ParameterType });
                        }
                        else
                            throw new Exception("Cannot resolve parameter type");
                    }
                    if (param.GetCustomAttribute<ConventionIsRequiredAttribute>() != null && valueToAdd == null)
                    {
                        throw new Exception($"{param.Name} is required");
                    }
                    handlerParams.Add(valueToAdd);
                }
                var res = methodInfo.Invoke(obj, handlerParams.ToArray());

                if (res is Task task)
                {
                    if(task.Exception!=null)
                        throw task.Exception;
                    System.Console.CancelKeyPress += (sender, e) =>
                    {
                        cts?.Cancel();
                        Task.WaitAll(new[] { task }, 10000);
                    };
                    AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                    {
                        cts?.Cancel();
						Task.WaitAll(new[] { task }, 10000);
                    };
                    Task.WaitAll(task);
                    return (int)(task.GetType().GetProperty("Result").GetValue(task));
                }
                else
                    return (int)res;
            });
        }

        private static bool _HasDuplicateHandleMethod(MethodInfo[] methods)
        {
            var groups = methods.GroupBy(m => m.Name);
            return groups.Any(gr => gr.Count() > 1);
        }

        private static void _RegisterOption(CommandLineApplication command, Type type, string name, string description, bool isRequired)
        {
            CommandOptionType optType;
            if (type == typeof(bool))
                optType = CommandOptionType.NoValue;
            else if (_IsExpectingSingleValue(type))
                optType = CommandOptionType.SingleValue;
            else if (_IsExpectingMultiValue(type))
                optType = CommandOptionType.MultipleValue;
            else
                return;
            var template = "--" + name;
            var opt = command.Option(template, description, optType);
            opt.Inherited = true;
            if (isRequired && opt.OptionType != CommandOptionType.NoValue)
                opt.IsRequired();
        }
        private static object _ExtractValueFromOption(Type type, CommandOption option)
        {
            if (type == typeof(bool))
            {
                return option.HasValue();
            }
            if (_IsExpectingSingleValue(type))
            {
                return _ParseSingleValue(type, option.Value());
            }
            if (_IsExpectingMultiValue(type))
            {
                return _ParseMultiValue(type, option.Values);
            }
            return null;
        }

        private static bool _IsExpectingSingleValue(Type type)
        {
            return type == typeof(string) || type == typeof(int) || type == typeof(float);
        }
        private static bool _IsExpectingMultiValue(Type type)
        {
            return type == typeof(List<string>) || type == typeof(List<int>) || type == typeof(List<float>);
        }
        private static object _ParseSingleValue(Type type, string value)
        {
            if (type == typeof(string)) return value;
            if (type == typeof(int)) return Convert.ToInt32(value);
            if (type == typeof(float)) return Convert.ToSingle(value);
            return null;
        }
        private static object _ParseMultiValue(Type type, List<string> value)
        {
            if (type == typeof(List<string>)) return value;
            if (type == typeof(List<int>)) return value.Select(val => Convert.ToInt32(val)).ToList();
            if (type == typeof(List<float>)) return value.Select(val => Convert.ToSingle(val)).ToList();
            return null;
        }
        private static string _PascalToKebab(string pascal)
        {
            var chars = pascal.ToCharArray();
            var newChars = new List<char>();
            foreach (var c in chars)
            {
                if (c >= 'A' && c <= 'Z')
                {
                    if (newChars.Count > 0)
                        newChars.Add('-');
                    newChars.Add((char)(c + ('a' - 'A')));
                }
                else
                    newChars.Add(c);
            }
            return new string(newChars.ToArray());
        }
    }
}
