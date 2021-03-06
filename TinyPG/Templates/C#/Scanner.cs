﻿// Generated by TinyPG v1.3 available at www.codeproject.com

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace <%Namespace%>
{
	#region Scanner

	public partial class Scanner
	{
		public string Input { get; set; }
		public int StartPosition { get; set; }
		public int EndPosition { get; set; }
		public string CurrentFile { get; set; }
		public int CurrentLine { get; set; }
		public int CurrentColumn { get; set; }
		public int CurrentPosition { get; set; }
		/// <summary>
		/// tokens that were skipped
		/// </summary>
		public List<Token> Skipped { get; set; }
		public Dictionary<TokenType, Regex> Patterns { get; set; }

		private Token LookAheadToken;
		private List<TokenType> Tokens;
		private List<TokenType> SkipList; // tokens to be skipped
<%FileAndLine%>

		public Scanner()
		{
			StartPosition = 0;
			EndPosition = 0;
			Regex regex;
			Patterns = new Dictionary<TokenType, Regex>();
			Tokens = new List<TokenType>();
			LookAheadToken = null;
			Skipped = new List<Token>();

			SkipList = new List<TokenType>();
<%SkipList%>
<%RegExps%>
		}

		public void Init(string input)
		{
			Init(input, "");
		}

		public void Init(string input, string fileName)
		{
			this.Input = input;
			StartPosition = 0;
			EndPosition = 0;
			CurrentFile = fileName;
			CurrentLine = 1;
			CurrentColumn = 1;
			CurrentPosition = 0;
			LookAheadToken = null;
		}

		public Token GetToken(TokenType type)
		{
			Token t = new Token(this.StartPosition, this.EndPosition);
			t.Type = type;
			return t;
		}

		/// <summary>
		/// executes a lookahead of the next token
		/// and will advance the scan on the input string
		/// </summary>
		/// <returns></returns>
		public Token Scan(params TokenType[] expectedtokens)
		{
			Token tok = LookAhead(expectedtokens); // temporarely retrieve the lookahead
			LookAheadToken = null; // reset lookahead token, so scanning will continue
			StartPosition = tok.EndPosition;
			EndPosition = tok.EndPosition; // set the tokenizer to the new scan position
			CurrentLine = tok.Line + (tok.Text.Length - tok.Text.Replace("\n", "").Length);
			CurrentFile = tok.File;
			return tok;
		}

		/// <summary>
		/// returns token with longest best match
		/// </summary>
		/// <returns></returns>
		public Token LookAhead(params TokenType[] expectedtokens)
		{
			int i;
			int startpos = StartPosition;
			int endpos = EndPosition;
			int currentline = CurrentLine;
			string currentFile = CurrentFile;
			Token tok = null;
			List<TokenType> scantokens;


			// this prevents double scanning and matching
			// increased performance
			if (LookAheadToken != null
				&& LookAheadToken.Type != TokenType._UNDETERMINED_
				&& LookAheadToken.Type != TokenType._NONE_) return LookAheadToken;

			// if no scantokens specified, then scan for all of them (= backward compatible)
			if (expectedtokens.Length == 0)
				scantokens = Tokens;
			else
			{
				scantokens = new List<TokenType>(expectedtokens);
				scantokens.AddRange(SkipList);
			}

			do
			{

				int len = -1;
				TokenType index = (TokenType)int.MaxValue;
				string input = Input.Substring(startpos);

				tok = new Token(startpos, endpos);

				for (i = 0; i < scantokens.Count; i++)
				{
					Regex r = Patterns[scantokens[i]];
					Match m = r.Match(input);
					if (m.Success && m.Index == 0 && ((m.Length > len) || (scantokens[i] < index && m.Length == len)))
					{
						len = m.Length;
						index = scantokens[i];
					}
				}

				if (index >= 0 && len >= 0)
				{
					tok.EndPosition = startpos + len;
					tok.Text = Input.Substring(tok.StartPosition, len);
					tok.Type = index;
				}
				else if (tok.StartPosition == tok.EndPosition)
				{
					if (tok.StartPosition < Input.Length)
						tok.Text = Input.Substring(tok.StartPosition, 1);
					else
						tok.Text = "EOF";
				}

				// Update the line and column count for error reporting.
				tok.File = currentFile;
				tok.Line = currentline;
				if (tok.StartPosition < Input.Length)
					tok.Column = tok.StartPosition - Input.LastIndexOf('\n', tok.StartPosition);

				if (SkipList.Contains(tok.Type))
				{
					startpos = tok.EndPosition;
					endpos = tok.EndPosition;
					currentline = tok.Line + (tok.Text.Length - tok.Text.Replace("\n", "").Length);
					currentFile = tok.File;
					Skipped.Add(tok);
				}
				else
				{
					// only assign to non-skipped tokens
					tok.Skipped = Skipped; // assign prior skips to this token
					Skipped = new List<Token>(); //reset skips
				}
<%FileAndLineCheck%>
			}
			while (SkipList.Contains(tok.Type));

			LookAheadToken = tok;
			return tok;
		}
	}

	#endregion

	#region Token

	public enum TokenType
	{
<%TokenType%>
	}

	public class Token<%IToken%>
	{
		public string File { get; set; }
		public int Line { get; set; }
		public int Column { get; set; }
		public int StartPosition { get; set; }
		public int EndPosition { get; set; }
		public string Text { get; set; }

		/// <summary>
		///  contains all prior skipped symbols
		/// </summary>
		public List<Token> Skipped { get; set; }

		public int Length { get { return EndPosition - StartPosition; } }

		[XmlAttribute]
		public TokenType Type { get; set; }

		public Token()
			: this(0, 0)
		{
		}

		public Token(int start, int end)
		{
			Type = TokenType._UNDETERMINED_;
			StartPosition = start;
			EndPosition = end;
			Text = ""; // must initialize with empty string, may cause null reference exceptions otherwise
		}

		public void UpdateRange(Token token)
		{
			if (token.StartPosition < this.StartPosition) this.StartPosition = token.StartPosition;
			if (token.EndPosition > this.EndPosition) this.EndPosition = token.EndPosition;
		}

		public override string ToString()
		{
			if (Text != null)
				return Type.ToString() + " '" + Text + "'";
			else
				return Type.ToString();
		}
	}

	#endregion
}
