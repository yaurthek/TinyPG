using System.Text;
using System.IO;
using TinyPG.Compiler;
using System.Text.RegularExpressions;

namespace TinyPG.CodeGenerators.CSharp
{
	public class ParseTreeGenerator : BaseGenerator, ICodeGenerator
	{
		internal ParseTreeGenerator()
			: base("ParseTree.cs")
		{
		}

		public string Generate(Grammar Grammar, bool Debug)
		{
			if (string.IsNullOrEmpty(Grammar.GetTemplatePath()))
				return null;

			// copy the parse tree file (optionally)
			string parsetree = File.ReadAllText(Grammar.GetTemplatePath() + templateName);
			string getvaluehelpers = "";
			string evalentrypoint = "";
			StringBuilder evalswitch = new StringBuilder();
			StringBuilder evalsymbols = new StringBuilder();
			StringBuilder evalmethods = new StringBuilder();

			bool generateEvaluationCode = Grammar.Directives["Evaluation"]["Generate"].ToLowerInvariant() == "true";

			if (generateEvaluationCode)
			{
				// build non terminal tokens
				foreach (Symbol s in Grammar.GetNonTerminals())
				{
					evalsymbols.AppendLine("				case TokenType." + s.Name + ":");
					evalsymbols.AppendLine("					Value = Eval" + s.Name + "(tree, paramlist);");
					//evalsymbols.AppendLine("				Value = Token.Text;");
					evalsymbols.AppendLine("					break;");

					evalmethods.AppendLine();
					evalmethods.AppendLine("		protected virtual object Eval" + s.Name + "(ParseTree tree, params object[] paramlist)");
					evalmethods.AppendLine("		{");
					if (s.CodeBlock != null)
					{
						// paste user code here
						evalmethods.AppendLine(FormatCodeBlock(s as NonTerminalSymbol));
					}
					else
					{
						if (s.Name == "Start") // return a nice warning message from root object.
							evalmethods.AppendLine("			return \"Could not interpret input; no semantics implemented.\";");
						else
							evalmethods.AppendLine("			foreach (var node in Nodes)\r\n" +
												   "				node.Eval(tree, paramlist);\r\n" +
												   "			return null;");

						// otherwise simply not implemented!
					}
					evalmethods.AppendLine("		}\r\n");
				}

				evalentrypoint = @"
		/// <summary>
		/// this is the entry point for executing and evaluating the parse tree.
		/// </summary>
		/// <param name=""paramlist"">additional optional input parameters</param>
		/// <returns>the output of the evaluation function</returns>
		public object Eval(params object[] paramlist)
		{
			return Nodes[0].Eval(this, paramlist);
		}";

				getvaluehelpers = @"
		protected object GetValue(ParseTree tree, TokenType type, int index)
		{
			return GetValue(tree, type, ref index);
		}

		protected object GetValue(ParseTree tree, TokenType type, ref int index)
		{
			object o = null;
			if (index < 0) return o;

			// left to right
			foreach (ParseNode node in Nodes)
			{
				if (node.Token.Type == type)
				{
					index--;
					if (index < 0)
					{
						o = node.Eval(tree);
						break;
					}
				}
			}
			return o;
		}";

				evalswitch.Append(
@"
		/// <summary>
		/// this implements the evaluation functionality, cannot be used directly
		/// </summary>
		/// <param name=""tree"">the parsetree itself</param>
		/// <param name=""paramlist"">optional input parameters</param>
		/// <returns>a partial result of the evaluation</returns>
		internal object Eval(ParseTree tree, params object[] paramlist)
		{
			object Value = null;

			switch (Token.Type)
			{
"

					+ evalsymbols.ToString() +

  @"
				default:
					Value = Token.Text;
					break;
			}
			return Value;
		}");

			} //generateEvaluationCode


			if (Debug)
			{
				parsetree = parsetree.Replace(@"<%Namespace%>", "TinyPG.Debug");
				parsetree = parsetree.Replace(@"<%ParseError%>", " : TinyPG.Debug.IParseError");
				parsetree = parsetree.Replace(@"<%ParseErrors%>", "List<TinyPG.Debug.IParseError>");
				parsetree = parsetree.Replace(@"<%IParseTree%>",
					", TinyPG.Debug.IParseTree" + (generateEvaluationCode ? ", TinyPG.Debug.IEvaluable" : ""));
				parsetree = parsetree.Replace(@"<%IParseNode%>", " : TinyPG.Debug.IParseNode");
				parsetree = parsetree.Replace(@"<%ITokenGet%>", "public IToken IToken { get {return (IToken)Token;} }");

				string inodes = "public List<IParseNode> INodes {get { return Nodes.ConvertAll<IParseNode>( new Converter<ParseNode, IParseNode>( delegate(ParseNode n) { return (IParseNode)n; })); }}\r\n\r\n";
				parsetree = parsetree.Replace(@"<%INodesGet%>", inodes);
			}
			else
			{
				parsetree = parsetree.Replace(@"<%Namespace%>", Grammar.Directives["TinyPG"]["Namespace"]);
				parsetree = parsetree.Replace(@"<%ParseError%>", "");
				parsetree = parsetree.Replace(@"<%ParseErrors%>", "List<ParseError>");
				parsetree = parsetree.Replace(@"<%IParseTree%>", "");
				parsetree = parsetree.Replace(@"<%IParseNode%>", "");
				parsetree = parsetree.Replace(@"<%ITokenGet%>", "");
				parsetree = parsetree.Replace(@"<%INodesGet%>", "");
			}

			parsetree = parsetree.Replace(@"<%EvalEntryPoint%>", evalentrypoint);
			parsetree = parsetree.Replace(@"<%EvalHelpers%>", getvaluehelpers);
			parsetree = parsetree.Replace(@"<%EvalSwitch%>", evalswitch.ToString());
			parsetree = parsetree.Replace(@"<%VirtualEvalMethods%>", evalmethods.ToString());

			return parsetree;
		}

		/// <summary>
		/// replaces $ variables with a c# statement
		/// the routine also implements some checks to see if $variables are matching with production symbols
		/// errors are added to the Error object.
		/// </summary>
		/// <param name="nts">non terminal and its production rule</param>
		/// <returns>a formated codeblock</returns>
		private string FormatCodeBlock(NonTerminalSymbol nts)
		{
			string codeblock = nts.CodeBlock;
			if (nts == null) return "";

			Regex var = new Regex(@"\$(?<var>[a-zA-Z_0-9]+)(\[(?<index>[^]]+)\])?", RegexOptions.Compiled);

			Symbols symbols = nts.DetermineProductionSymbols();


			Match match = var.Match(codeblock);
			while (match.Success)
			{
				Symbol s = symbols.Find(match.Groups["var"].Value);
				if (s == null)
				{
					//TOD: handle error situation
					//Errors.Add("Variable $" + match.Groups["var"].Value + " cannot be matched.");
					break; // error situation
				}
				string indexer = "0";
				if (match.Groups["index"].Value.Length > 0)
				{
					indexer = match.Groups["index"].Value;
				}

				string replacement = "this.GetValue(tree, TokenType." + s.Name + ", " + indexer + ")";

				codeblock = codeblock.Substring(0, match.Captures[0].Index) + replacement + codeblock.Substring(match.Captures[0].Index + match.Captures[0].Length);
				match = var.Match(codeblock);
			}

			codeblock = "			" + codeblock.Replace("\n", "\r\n		");
			return codeblock;
		}
	}

}
