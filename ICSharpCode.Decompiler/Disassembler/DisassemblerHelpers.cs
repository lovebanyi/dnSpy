﻿// Copyright (c) 2011 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using ICSharpCode.NRefactory;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ICSharpCode.Decompiler.Disassembler
{
	public enum ILNameSyntax
	{
		/// <summary>
		/// class/valuetype + TypeName (built-in types use keyword syntax)
		/// </summary>
		Signature,
		/// <summary>
		/// Like signature, but always refers to type parameters using their position
		/// </summary>
		SignatureNoNamedTypeParameters,
		/// <summary>
		/// [assembly]Full.Type.Name (even for built-in types)
		/// </summary>
		TypeName,
		/// <summary>
		/// Name (but built-in types use keyword syntax)
		/// </summary>
		ShortTypeName
	}
	
	public static class DisassemblerHelpers
	{
		public static void WriteOffsetReference(ITextOutput writer, Instruction instruction, TextTokenType tokenType = TextTokenType.Label)
		{
			writer.WriteReference(DnlibExtensions.OffsetToString(instruction.GetOffset()), instruction, tokenType);
		}
		
		public static void WriteTo(this ExceptionHandler exceptionHandler, ITextOutput writer)
		{
			writer.Write("Try", TextTokenType.Keyword);
			writer.WriteSpace();
			WriteOffsetReference(writer, exceptionHandler.TryStart);
			writer.Write('-', TextTokenType.Operator);
			WriteOffsetReference(writer, exceptionHandler.TryEnd);
			writer.WriteSpace();
			writer.Write(exceptionHandler.HandlerType.ToString(), TextTokenType.Keyword);
			if (exceptionHandler.FilterStart != null) {
				writer.WriteSpace();
				WriteOffsetReference(writer, exceptionHandler.FilterStart);
				writer.WriteSpace();
				writer.Write("handler", TextTokenType.Keyword);
				writer.WriteSpace();
			}
			if (exceptionHandler.CatchType != null) {
				writer.WriteSpace();
				exceptionHandler.CatchType.WriteTo(writer);
			}
			writer.WriteSpace();
			WriteOffsetReference(writer, exceptionHandler.HandlerStart);
			writer.Write('-', TextTokenType.Operator);
			WriteOffsetReference(writer, exceptionHandler.HandlerEnd);
		}
		
		public static void WriteTo(this Instruction instruction, ITextOutput writer, Func<OpCode, string> getOpCodeDocumentation)
		{
			writer.WriteDefinition(DnlibExtensions.OffsetToString(instruction.GetOffset()), instruction, TextTokenType.Label, false);
			writer.Write(':', TextTokenType.Operator);
			writer.WriteSpace();
			writer.WriteReference(instruction.OpCode.Name, instruction.OpCode, TextTokenType.OpCode);
			if (instruction.Operand != null) {
				writer.WriteSpace();
				if (instruction.OpCode == OpCodes.Ldtoken) {
					var member = instruction.Operand as IMemberRef;
					if (member != null && member.IsMethod) {
						writer.Write("method", TextTokenType.Keyword);
						writer.WriteSpace();
					}
					else if (member != null && member.IsField) {
						writer.Write("field", TextTokenType.Keyword);
						writer.WriteSpace();
					}
				}
				WriteOperand(writer, instruction.Operand);
			}
			if (getOpCodeDocumentation != null) {
				var doc = getOpCodeDocumentation(instruction.OpCode);
				if (doc != null) {
					writer.Write("\t", TextTokenType.Text);
					writer.Write("// " + doc, TextTokenType.Comment);
				}
			}
		}
		
		static void WriteLabelList(ITextOutput writer, IList<Instruction> instructions)
		{
			writer.Write("(", TextTokenType.Operator);
			for(int i = 0; i < instructions.Count; i++) {
				if (i != 0) {
					writer.Write(',', TextTokenType.Operator);
					writer.WriteSpace();
				}
				WriteOffsetReference(writer, instructions[i]);
			}
			writer.Write(")", TextTokenType.Operator);
		}
		
		static string ToInvariantCultureString(object value)
		{
			if (value == null)
				return "<<<NULL>>>";
			IConvertible convertible = value as IConvertible;
			return(null != convertible)
				? convertible.ToString(System.Globalization.CultureInfo.InvariantCulture)
				: value.ToString();
		}
		
		public static void WriteMethodTo(this IMethod method, ITextOutput writer)
		{
			if (method == null)
				return;
			MethodSig sig = method.MethodSig;
			if (sig == null)
				return;
			if (sig.ExplicitThis) {
				writer.Write("instance", TextTokenType.Keyword);
				writer.WriteSpace();
				writer.Write("explicit", TextTokenType.Keyword);
				writer.WriteSpace();
			}
			else if (sig.HasThis) {
				writer.Write("instance", TextTokenType.Keyword);
				writer.WriteSpace();
			}
			sig.RetType.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.WriteSpace();
			if (method.DeclaringType != null) {
				method.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName);
				writer.Write("::", TextTokenType.Operator);
			}
			MethodDef md = method as MethodDef;
			if (md != null && md.IsCompilerControlled) {
				writer.WriteReference(Escape(method.Name + "$PST" + method.MDToken.ToInt32().ToString("X8")), method, TextTokenHelper.GetTextTokenType(method));
			} else {
				writer.WriteReference(Escape(method.Name), method, TextTokenHelper.GetTextTokenType(method));
			}
			MethodSpec gim = method as MethodSpec;
			if (gim != null && gim.GenericInstMethodSig != null) {
				writer.Write('<', TextTokenType.Operator);
				for (int i = 0; i < gim.GenericInstMethodSig.GenericArguments.Count; i++) {
					if (i > 0) {
						writer.Write(',', TextTokenType.Operator);
						writer.WriteSpace();
					}
					gim.GenericInstMethodSig.GenericArguments[i].WriteTo(writer);
				}
				writer.Write('>', TextTokenType.Operator);
			}
			writer.Write("(", TextTokenType.Operator);
			var parameters = sig.GetParameters();
			for(int i = 0; i < parameters.Count; ++i) {
				if (i > 0) {
					writer.Write(',', TextTokenType.Operator);
					writer.WriteSpace();
				}
				parameters[i].WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			}
			writer.Write(")", TextTokenType.Operator);
		}

		public static void WriteTo(this MethodSig sig, ITextOutput writer)
		{
			if (sig.ExplicitThis) {
				writer.Write("instance", TextTokenType.Keyword);
				writer.WriteSpace();
				writer.Write("explicit", TextTokenType.Keyword);
				writer.WriteSpace();
			}
			else if (sig.HasThis) {
				writer.Write("instance", TextTokenType.Keyword);
				writer.WriteSpace();
			}
			sig.RetType.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.WriteSpace();
			writer.Write('(', TextTokenType.Operator);
			var parameters = sig.GetParameters();
			for(int i = 0; i < parameters.Count; ++i) {
				if (i > 0) {
					writer.Write(',', TextTokenType.Operator);
					writer.WriteSpace();
				}
				parameters[i].WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			}
			writer.Write(")", TextTokenType.Operator);
		}
		
		static void WriteFieldTo(this IField field, ITextOutput writer)
		{
			if (field == null || field.FieldSig == null)
				return;
			field.FieldSig.Type.WriteTo(writer, ILNameSyntax.SignatureNoNamedTypeParameters);
			writer.WriteSpace();
			field.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName);
			writer.Write("::", TextTokenType.Operator);
			writer.WriteReference(Escape(field.Name), field, TextTokenHelper.GetTextTokenType(field));
		}
		
		static bool IsValidIdentifierCharacter(char c)
		{
			return c == '_' || c == '$' || c == '@' || c == '?' || c == '`';
		}
		
		static bool IsValidIdentifier(string identifier)
		{
			if (string.IsNullOrEmpty(identifier))
				return false;
			if (!(char.IsLetter(identifier[0]) || IsValidIdentifierCharacter(identifier[0]))) {
				// As a special case, .ctor and .cctor are valid despite starting with a dot
				return identifier == ".ctor" || identifier == ".cctor";
			}
			for (int i = 1; i < identifier.Length; i++) {
				if (!(char.IsLetterOrDigit(identifier[i]) || IsValidIdentifierCharacter(identifier[i]) || identifier[i] == '.'))
					return false;
			}
			return true;
		}
		
		static readonly HashSet<string> ilKeywords = BuildKeywordList(
			"abstract", "algorithm", "alignment", "ansi", "any", "arglist",
			"array", "as", "assembly", "assert", "at", "auto", "autochar", "beforefieldinit",
			"blob", "blob_object", "bool", "brnull", "brnull.s", "brzero", "brzero.s", "bstr",
			"bytearray", "byvalstr", "callmostderived", "carray", "catch", "cdecl", "cf",
			"char", "cil", "class", "clsid", "const", "currency", "custom", "date", "decimal",
			"default", "demand", "deny", "endmac", "enum", "error", "explicit", "extends", "extern",
			"false", "famandassem", "family", "famorassem", "fastcall", "fault", "field", "filetime",
			"filter", "final", "finally", "fixed", "float", "float32", "float64", "forwardref",
			"fromunmanaged", "handler", "hidebysig", "hresult", "idispatch", "il", "illegal",
			"implements", "implicitcom", "implicitres", "import", "in", "inheritcheck", "init",
			"initonly", "instance", "int", "int16", "int32", "int64", "int8", "interface", "internalcall",
			"iunknown", "lasterr", "lcid", "linkcheck", "literal", "localloc", "lpstr", "lpstruct", "lptstr",
			"lpvoid", "lpwstr", "managed", "marshal", "method", "modopt", "modreq", "native", "nested",
			"newslot", "noappdomain", "noinlining", "nomachine", "nomangle", "nometadata", "noncasdemand",
			"noncasinheritance", "noncaslinkdemand", "noprocess", "not", "not_in_gc_heap", "notremotable",
			"notserialized", "null", "nullref", "object", "objectref", "opt", "optil", "out",
			"permitonly", "pinned", "pinvokeimpl", "prefix1", "prefix2", "prefix3", "prefix4", "prefix5", "prefix6",
			"prefix7", "prefixref", "prejitdeny", "prejitgrant", "preservesig", "private", "privatescope", "protected",
			"public", "record", "refany", "reqmin", "reqopt", "reqrefuse", "reqsecobj", "request", "retval",
			"rtspecialname", "runtime", "safearray", "sealed", "sequential", "serializable", "special", "specialname",
			"static", "stdcall", "storage", "stored_object", "stream", "streamed_object", "string", "struct",
			"synchronized", "syschar", "sysstring", "tbstr", "thiscall", "tls", "to", "true", "typedref",
			"unicode", "unmanaged", "unmanagedexp", "unsigned", "unused", "userdefined", "value", "valuetype",
			"vararg", "variant", "vector", "virtual", "void", "wchar", "winapi", "with", "wrapper",
			
			// These are not listed as keywords in spec, but ILAsm treats them as such
			"property", "type", "flags", "callconv", "strict"
		);
		
		static HashSet<string> BuildKeywordList(params string[] keywords)
		{
			HashSet<string> s = new HashSet<string>(keywords);
			foreach (var field in typeof(OpCodes).GetFields()) {
				if (field.FieldType != typeof(OpCode))
					continue;
				OpCode opCode = (OpCode)field.GetValue(null);
				if (opCode.OpCodeType != OpCodeType.Nternal)
					s.Add(opCode.Name);
			}
			return s;
		}
		
		public static string Escape(string identifier)
		{
			if (IsValidIdentifier(identifier) && !ilKeywords.Contains(identifier)) {
				return identifier;
			} else {
				// The ECMA specification says that ' inside SQString should be ecaped using an octal escape sequence,
				// but we follow Microsoft's ILDasm and use \'.
				return "'" + NRefactory.CSharp.TextWriterTokenWriter.ConvertString(identifier).Replace("'", "\\'") + "'";
			}
		}
		
		public static void WriteTo(this TypeSig type, ITextOutput writer, ILNameSyntax syntax = ILNameSyntax.Signature)
		{
			type.WriteTo(writer, syntax, 0);
		}

		const int MAX_CONVERTTYPE_DEPTH = 50;
		public static void WriteTo(this TypeSig type, ITextOutput writer, ILNameSyntax syntax, int depth)
		{
			if (depth++ > MAX_CONVERTTYPE_DEPTH)
				return;
			ILNameSyntax syntaxForElementTypes = syntax == ILNameSyntax.SignatureNoNamedTypeParameters ? syntax : ILNameSyntax.Signature;
			if (type is PinnedSig) {
				((PinnedSig)type).Next.WriteTo(writer, syntaxForElementTypes, depth);
				writer.WriteSpace();
				writer.Write("pinned", TextTokenType.Keyword);
			} else if (type is ArraySig) {
				ArraySig at = (ArraySig)type;
				at.Next.WriteTo(writer, syntaxForElementTypes, depth);
				writer.Write('[', TextTokenType.Operator);
				for (int i = 0; i < at.Rank; i++)
				{
					if (i != 0) {
						writer.Write(',', TextTokenType.Operator);
						writer.WriteSpace();
					}
					int? lower = i < at.LowerBounds.Count ? at.LowerBounds[i] : (int?)null;
					uint? size = i < at.Sizes.Count ? at.Sizes[i] : (uint?)null;
					if (lower != null)
					{
						writer.Write(lower.ToString(), TextTokenType.Number);
						if (size != null) {
							writer.Write("..", TextTokenType.Operator);
							writer.Write((lower.Value + (int)size.Value - 1).ToString(), TextTokenType.Number);
						}
						else
							writer.Write("...", TextTokenType.Operator);
					}
				}
				writer.Write(']', TextTokenType.Operator);
			} else if (type is SZArraySig) {
				SZArraySig at = (SZArraySig)type;
				at.Next.WriteTo(writer, syntaxForElementTypes, depth);
				writer.Write("[]", TextTokenType.Operator);
			} else if (type is GenericSig) {
				if (((GenericSig)type).IsMethodVar)
					writer.Write("!!", TextTokenType.Operator);
				else
					writer.Write("!", TextTokenType.Operator);
				string typeName = type.TypeName;
				if (string.IsNullOrEmpty(typeName) || typeName[0] == '!' || syntax == ILNameSyntax.SignatureNoNamedTypeParameters)
					writer.Write(((GenericSig)type).Number.ToString(), TextTokenType.Number);
				else
					writer.Write(Escape(typeName), TextTokenHelper.GetTextTokenType(type));
			} else if (type is ByRefSig) {
				((ByRefSig)type).Next.WriteTo(writer, syntaxForElementTypes, depth);
				writer.Write('&', TextTokenType.Operator);
			} else if (type is PtrSig) {
				((PtrSig)type).Next.WriteTo(writer, syntaxForElementTypes, depth);
				writer.Write('*', TextTokenType.Operator);
			} else if (type is GenericInstSig) {
				((GenericInstSig)type).GenericType.WriteTo(writer, syntaxForElementTypes, depth);
				writer.Write('<', TextTokenType.Operator);
				var arguments = ((GenericInstSig)type).GenericArguments;
				for (int i = 0; i < arguments.Count; i++) {
					if (i > 0) {
						writer.Write(',', TextTokenType.Operator);
						writer.WriteSpace();
					}
					arguments[i].WriteTo(writer, syntaxForElementTypes, depth);
				}
				writer.Write('>', TextTokenType.Operator);
			} else if (type is CModOptSig) {
				((ModifierSig)type).Next.WriteTo(writer, syntax, depth);
				writer.WriteSpace();
				writer.Write("modopt", TextTokenType.Keyword);
				writer.Write('(', TextTokenType.Operator);
				((ModifierSig)type).Modifier.WriteTo(writer, ILNameSyntax.TypeName, depth);
				writer.Write(')', TextTokenType.Operator);
				writer.WriteSpace();
			}
			else if (type is CModReqdSig) {
				((ModifierSig)type).Next.WriteTo(writer, syntax, depth);
				writer.WriteSpace();
				writer.Write("modreq", TextTokenType.Keyword);
				writer.Write('(', TextTokenType.Operator);
				((ModifierSig)type).Modifier.WriteTo(writer, ILNameSyntax.TypeName, depth);
				writer.Write(')', TextTokenType.Operator);
				writer.WriteSpace();
			}
			else if (type is TypeDefOrRefSig) {
				WriteTo(((TypeDefOrRefSig)type).TypeDefOrRef, writer, syntax, depth);
			} else if (type is FnPtrSig) {
				WriteTo(type.ToTypeDefOrRef(), writer, syntax, depth);
			}
			//TODO: SentinelSig
		}

		public static void WriteTo(this ITypeDefOrRef type, ITextOutput writer, ILNameSyntax syntax = ILNameSyntax.Signature)
		{
			type.WriteTo(writer, syntax, 0);
		}

		public static void WriteTo(this ITypeDefOrRef type, ITextOutput writer, ILNameSyntax syntax, int depth)
		{
			if (depth++ > MAX_CONVERTTYPE_DEPTH || type == null)
				return;
			var ts = type as TypeSpec;
			if (ts != null && !(ts.TypeSig is FnPtrSig)) {
				WriteTo(((TypeSpec)type).TypeSig, writer, syntax, depth);
				return;
			}
			string typeFullName = type.FullName;
			string typeName = type.Name.String;
			if (ts != null) {
				var fnPtrSig = ts.TypeSig as FnPtrSig;
				typeFullName = DnlibExtensions.GetFnPtrFullName(fnPtrSig);
				typeName = DnlibExtensions.GetFnPtrName(fnPtrSig);
			}
			string name = PrimitiveTypeName(typeFullName);
			if (syntax == ILNameSyntax.ShortTypeName) {
				if (name != null)
					WriteKeyword(writer, name);
				else
					writer.WriteReference(Escape(typeName), type, TextTokenHelper.GetTextTokenType(type));
			} else if ((syntax == ILNameSyntax.Signature || syntax == ILNameSyntax.SignatureNoNamedTypeParameters) && name != null) {
				WriteKeyword(writer, name);
			} else {
				if (syntax == ILNameSyntax.Signature || syntax == ILNameSyntax.SignatureNoNamedTypeParameters) {
					writer.Write(DnlibExtensions.IsValueType(type) ? "valuetype" : "class", TextTokenType.Keyword);
					writer.WriteSpace();
				}

				if (type.DeclaringType != null) {
					type.DeclaringType.WriteTo(writer, ILNameSyntax.TypeName, depth);
					writer.Write('/', TextTokenType.Operator);
					writer.WriteReference(Escape(typeName), type, TextTokenHelper.GetTextTokenType(type));
				} else {
					if (!(type is TypeDef) && type.Scope != null && !(type is TypeSpec)) {
						writer.Write('[', TextTokenType.Operator);
						writer.Write(Escape(type.Scope.GetScopeName()), TextTokenType.ILModule);
						writer.Write(']', TextTokenType.Operator);
					}
					writer.WriteReference(Escape(typeFullName), type, TextTokenHelper.GetTextTokenType(type));
				}
			}
		}

		internal static void WriteKeyword(ITextOutput writer, string name)
		{
			var parts = name.Split(' ');
			for (int i = 0; i < parts.Length; i++) {
				if (i > 0)
					writer.WriteSpace();
				writer.Write(parts[i], TextTokenType.Keyword);
			}
		}
		
		public static void WriteOperand(ITextOutput writer, object operand)
		{
			Instruction targetInstruction = operand as Instruction;
			if (targetInstruction != null) {
				WriteOffsetReference(writer, targetInstruction);
				return;
			}
			
			IList<Instruction> targetInstructions = operand as IList<Instruction>;
			if (targetInstructions != null) {
				WriteLabelList(writer, targetInstructions);
				return;
			}
			
			Local variable = operand as Local;
			if (variable != null) {
				if (string.IsNullOrEmpty(variable.Name))
					writer.WriteReference(variable.Index.ToString(), variable, TextTokenType.Number);
				else
					writer.WriteReference(Escape(variable.Name), variable, TextTokenType.Local);
				return;
			}
			
			Parameter paramRef = operand as Parameter;
			if (paramRef != null) {
				if (string.IsNullOrEmpty(paramRef.Name)) {
					if (paramRef.IsHiddenThisParameter)
						writer.WriteReference("<hidden-this>", paramRef, TextTokenType.Parameter);
					else
						writer.WriteReference(paramRef.MethodSigIndex.ToString(), paramRef, TextTokenType.Parameter);
				}
				else
					writer.WriteReference(Escape(paramRef.Name), paramRef, TextTokenType.Parameter);
				return;
			}
			
			MemberRef memberRef = operand as MemberRef;
			if (memberRef != null) {
				if (memberRef.IsMethodRef)
					memberRef.WriteMethodTo(writer);
				else
					memberRef.WriteFieldTo(writer);
				return;
			}
			
			MethodDef methodDef = operand as MethodDef;
			if (methodDef != null) {
				methodDef.WriteMethodTo(writer);
				return;
			}
			
			FieldDef fieldDef = operand as FieldDef;
			if (fieldDef != null) {
				fieldDef.WriteFieldTo(writer);
				return;
			}
			
			ITypeDefOrRef typeRef = operand as ITypeDefOrRef;
			if (typeRef != null) {
				typeRef.WriteTo(writer, ILNameSyntax.TypeName);
				return;
			}
			
			IMethod method = operand as IMethod;
			if (method != null) {
				method.WriteMethodTo(writer);
				return;
			}

			MethodSig sig = operand as MethodSig;
			if (sig != null) {
				sig.WriteTo(writer);
				return;
			}
			
			string s = operand as string;
			if (s != null) {
				writer.Write("\"" + NRefactory.CSharp.TextWriterTokenWriter.ConvertString(s) + "\"", TextTokenType.String);
			} else if (operand is char) {
				writer.Write(((int)(char)operand).ToString(), TextTokenType.Number);
			} else if (operand is float) {
				float val = (float)operand;
				if (val == 0) {
					if (1 / val == float.NegativeInfinity) {
						// negative zero is a special case
						writer.Write("-0.0", TextTokenType.Number);
					}
					else
						writer.Write("0.0", TextTokenType.Number);
				} else if (float.IsInfinity(val) || float.IsNaN(val)) {
					byte[] data = BitConverter.GetBytes(val);
					writer.Write('(', TextTokenType.Operator);
					for (int i = 0; i < data.Length; i++) {
						if (i > 0)
							writer.WriteSpace();
						writer.Write(data[i].ToString("X2"), TextTokenType.Number);
					}
					writer.Write(')', TextTokenType.Operator);
				} else {
					writer.Write(val.ToString("R", System.Globalization.CultureInfo.InvariantCulture), TextTokenType.Number);
				}
			} else if (operand is double) {
				double val = (double)operand;
				if (val == 0) {
					if (1 / val == double.NegativeInfinity) {
						// negative zero is a special case
						writer.Write("-0.0", TextTokenType.Number);
					}
					else
						writer.Write("0.0", TextTokenType.Number);
				} else if (double.IsInfinity(val) || double.IsNaN(val)) {
					byte[] data = BitConverter.GetBytes(val);
					writer.Write('(', TextTokenType.Operator);
					for (int i = 0; i < data.Length; i++) {
						if (i > 0)
							writer.WriteSpace();
						writer.Write(data[i].ToString("X2"), TextTokenType.Number);
					}
					writer.Write(')', TextTokenType.Operator);
				} else {
					writer.Write(val.ToString("R", System.Globalization.CultureInfo.InvariantCulture), TextTokenType.Number);
				}
			} else if (operand is bool) {
				writer.Write((bool)operand ? "true" : "false", TextTokenType.Keyword);
			} else {
				s = ToInvariantCultureString(operand);
				writer.Write(s, TextTokenHelper.GetTextTokenType(operand));
			}
		}
		
		public static string PrimitiveTypeName(string fullName)
		{
			switch (fullName) {
				case "System.SByte":
					return "int8";
				case "System.Int16":
					return "int16";
				case "System.Int32":
					return "int32";
				case "System.Int64":
					return "int64";
				case "System.Byte":
					return "uint8";
				case "System.UInt16":
					return "uint16";
				case "System.UInt32":
					return "uint32";
				case "System.UInt64":
					return "uint64";
				case "System.Single":
					return "float32";
				case "System.Double":
					return "float64";
				case "System.Void":
					return "void";
				case "System.Boolean":
					return "bool";
				case "System.String":
					return "string";
				case "System.Char":
					return "char";
				case "System.Object":
					return "object";
				case "System.IntPtr":
					return "native int";
				default:
					return null;
			}
		}
	}
}
