using Mono.Cecil;
using Mono.Cecil.Cil;

if (args.Length != 3)
{
    Console.WriteLine("usage: Patch <original Launcher.exe> <LauncherSavedLogin.dll> <patched Launcher.exe>");
    return 1;
}

var (input, helperPath, output) = (args[0], args[1], args[2]);

var frameworkDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET", "Framework", "v4.0.30319");
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(frameworkDirectory);
resolver.AddSearchDirectory(Path.Combine(frameworkDirectory, "WPF"));
resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(helperPath))!);

using var module = ModuleDefinition.ReadModule(input, new ReaderParameters { AssemblyResolver = resolver });
using var helperModule = ModuleDefinition.ReadModule(helperPath, new ReaderParameters { AssemblyResolver = resolver });

var main = module.GetType("Launcher.Main") ?? throw new InvalidOperationException("Launcher.Main not found");

if (main.Methods.SelectMany(method => method.HasBody ? method.Body.Instructions : []).Any(instruction =>
        instruction.Operand is MethodReference { DeclaringType.Name: "SavedLogin" }))
{
    Console.WriteLine("The launcher is already patched.");
    return 1;
}

var helper = helperModule.GetType("LauncherSavedLogin.SavedLogin");
var attach = module.ImportReference(helper.Methods.Single(method => method.Name == "Attach"));
var loginFinished = module.ImportReference(helper.Methods.Single(method => method.Name == "LoginFinished"));

FieldDefinition Field(string name) => main.Fields.Single(field => field.Name == name);

var loaded = main.Methods.Single(method => method.Name == "Window_Loaded");
var returns = loaded.Body.Instructions.Where(instruction => instruction.OpCode == OpCodes.Ret).ToList();

if (returns.Count != 1)
    throw new InvalidOperationException($"Window_Loaded has {returns.Count} returns");

var il = loaded.Body.GetILProcessor();
var last = returns[0];
last.OpCode = OpCodes.Ldarg_0;
last.Operand = null;
il.InsertAfter(last, il.Create(OpCodes.Ret));
var cursor = last;

foreach (var instruction in new[]
{
    il.Create(OpCodes.Ldfld, Field("panelLogin")),
    il.Create(OpCodes.Ldarg_0),
    il.Create(OpCodes.Ldfld, Field("txtUsername")),
    il.Create(OpCodes.Ldarg_0),
    il.Create(OpCodes.Ldfld, Field("txtPassword")),
    il.Create(OpCodes.Call, attach)
})
{
    il.InsertAfter(cursor, instruction);
    cursor = instruction;
}

var finished = main.Methods.Single(method => method.Name == "OnInitialLoginComplete");
il = finished.Body.GetILProcessor();
var first = finished.Body.Instructions[0];

foreach (var instruction in new[]
{
    il.Create(OpCodes.Ldarg_1),
    il.Create(OpCodes.Ldarg_0),
    il.Create(OpCodes.Ldfld, Field("txtUsername")),
    il.Create(OpCodes.Ldarg_0),
    il.Create(OpCodes.Ldfld, Field("txtPassword")),
    il.Create(OpCodes.Call, loginFinished)
})
    il.InsertBefore(first, instruction);

module.Write(output);
Console.WriteLine($"Patched: {output}");
return 0;
