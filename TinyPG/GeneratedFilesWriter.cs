using System;
using System.Collections.Generic;
using System.Text;
using TinyPG.CodeGenerators;
using TinyPG.Compiler;
using System.IO;

namespace TinyPG
{
	public class GeneratedFilesWriter
	{

		private Grammar grammar = null;

		public GeneratedFilesWriter(Grammar grammar)
		{
			this.grammar = grammar;
		}

		public bool Generate(bool debug, out string error)
		{

			ICodeGenerator generator;

			string language = grammar.Directives["TinyPG"]["Language"];
			foreach (Directive d in grammar.Directives)
			{
				generator = CodeGeneratorFactory.CreateGenerator(d.Name, language);

				if (generator != null && d.ContainsKey("FileName"))
				{
					generator.FileName = d["FileName"];
				}

				if (generator != null && d["Generate"].ToLower() == "true")
				{
					string folder = grammar.GetOutputPath();
					if (folder != null && generator.FileName != null)
					{
						File.WriteAllText(
						Path.Combine(grammar.GetOutputPath(), generator.FileName),
						generator.Generate(grammar, debug));
					}
					else
					{
						error = "The output path does not exist!";
						return false;
					}
				}
			}
			error = null;
			return true;

		}


	}
}
