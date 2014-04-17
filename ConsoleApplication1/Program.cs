using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApplication1
{
    class Program
    {
        #region tokens
        enum TokenType
        {
            String,
            Number,
            Identifier,
            Keyword,
            Symbol
        }

        class Token
        {
            public TokenType Type;
            public readonly int Line;
            public readonly int Column;
            public readonly string Value;
            public Token Next;

            public Token(TokenType type, string value, int line, int column)
            {
                Type = type;
                Line = line;
                Column = column;
                Value = value;
            }
        }

        static Regex regTokens = new Regex(@"(?:(?'str'""(?:[^""]|"""")*"")|" +
                                            @"(?'num'\d+(?:\.\d+)?)|" +
                                            @"(?'kwd'sub|var|for|if|else|while)|" +
                                            @"(?'id'[\w_][\w\d_]*)|" +
                                            @"(?'sym'[-+*/%&^|<>!=]=|&&|\|\||<<|>>|[-+~/*%&^|?:=(){};,<>]))", RegexOptions.Compiled);

        static Token Tokenize(string[] lines)
        {
            Token first = null;
            Token last = null;
            if (lines!=null && lines.Length>0)
            {
                for (int line = 0; line < lines.Length; line++)
                {
                    string text = lines[line];
                    var matches = regTokens.Matches(text);
                    foreach (Match match in matches)
                    {
                        if (match.Success)
                        {
                            Token token = null;
                            var groups = match.Groups;
                            if (groups["str"].Success)
                                token = new Token(TokenType.String, match.Value, line, match.Index);
                            else if (groups["num"].Success)
                                token = new Token(TokenType.Number, match.Value, line, match.Index);
                            else if (groups["kwd"].Success)
                                token = new Token(TokenType.Keyword, match.Value, line, match.Index);
                            else if (groups["id"].Success)
                                token = new Token(TokenType.Identifier, match.Value, line, match.Index);
                            else if (groups["sym"].Success)
                                token = new Token(TokenType.Symbol, match.Value, line, match.Index);
                            if (token == null)
                                throw new ApplicationException("Unknown token");
                            Console.WriteLine("{0} = {1}", match.Index, match.Value);
                            if (last != null)
                                last.Next = token;
                            last = token;
                            if (first == null)
                                first = token;
                        }
                    }
                }
            }
            return first;
        }
        #endregion tokens

        #region errors
        class ParserException : ApplicationException
        {
            public ParserException(Token token, string message) :
                base(token==null ? message : string.Format( "({0},{1}): {2}", token.Line + 1, token.Column + 1, message)) { }
        }

        const string _errUnexpectedEndOfFile = "unexpected end of file";
        const string _errIdentifierExpected = "identifier expected";
        const string _errStatementExpected = "statement expected";
        const string _errExpressionExpected = "expression expected";
        const string _errAssignmentOperatorExpected = "assignment operator expected";
        const string _errSubExpressionExpected = "sub-expression expected";
        const string _errfmt_0_Expected = "{0} expected";
        #endregion errors

        #region expressions
        class Expr
        {
        }

        class Expr_Num : Expr
        {
        }

        #region unary and binary operators

        class BaseOp
        {
            public readonly string Op;

            public BaseOp(string op)
            {
                Op = op;
            }
        }

        class BinOp : BaseOp
        {
            public BinOp(string op) : base(op) { }
        }

        class BinOpMul : BinOp
        {
            public BinOpMul() : base("*") { }
        }

        class BinOpDiv : BinOp
        {
            public BinOpDiv() : base("/") { }
        }

        class BinOpMod : BinOp
        {
            public BinOpMod() : base("%") { }
        }

        class BinOpAdd : BinOp
        {
            public BinOpAdd() : base("+") { }
        }

        class BinOpSub : BinOp
        {
            public BinOpSub() : base("-") { }
        }

        class BinOpShL : BinOp
        {
            public BinOpShL() : base("<<") { }
        }

        class BinOpShR : BinOp
        {
            public BinOpShR() : base(">>") { }
        }

        class BinOpLess : BinOp
        {
            public BinOpLess() : base("<") { }
        }

        class BinOpMore : BinOp
        {
            public BinOpMore() : base(">") { }
        }

        class BinOpLessEq : BinOp
        {
            public BinOpLessEq() : base("<=") { }
        }

        class BinOpMoreEq : BinOp
        {
            public BinOpMoreEq() : base(">=") { }
        }

        class BinOpEq : BinOp
        {
            public BinOpEq() : base("==") { }
        }

        class BinOpNotEq : BinOp
        {
            public BinOpNotEq() : base("!=") { }
        }

        class BinOpAnd : BinOp
        {
            public BinOpAnd() : base("&") { }
        }

        class BinOpXor : BinOp
        {
            public BinOpXor() : base("^") { }
        }

        class BinOpOr : BinOp
        {
            public BinOpOr() : base("|") { }
        }

        class BinOpLogAnd : BinOp
        {
            public BinOpLogAnd() : base("&&") { }
        }

        class BinOpLogOr : BinOp
        {
            public BinOpLogOr() : base("||") { }
        }

        class UnOp : BaseOp
        {
            public UnOp(string op) : base(op) { }
        }

        class UnOpLogNeg : UnOp
        {
            public UnOpLogNeg() : base("!") { }
        }

        class UnOpNeg : UnOp
        {
            public UnOpNeg() : base("-") { }
        }

        class UnOpCompl : UnOp
        {
            public UnOpCompl() : base("~") { }
        }

        abstract class OpHelper
        {
            public abstract Expr_Num Match(ref Token token);
        }

        class UnaryOpHelper : OpHelper
        {
            public UnOp[] Ops { get; private set; }

            public UnaryOpHelper(params UnOp[] ops)
            {
                Debug.Assert(ops.Length > 0);
                Ops = ops;
            }

            public override Expr_Num Match(ref Token token)
            {
                foreach (var op in Ops)
                {
                    if (MatchSymbol(ref token, op.Op, false))
                    {
                        var expr = Match(ref token);
                        if (expr == null)
                            throw new ParserException(token, _errSubExpressionExpected);

                        return new Expr_UnOp(op, expr);
                    }
                }
                return MatchPrimExpr(ref token);
            }
        }

        class BinOpHelper : OpHelper
        {
            public OpHelper Next { get; private set; }
            public BinOp[] Ops { get; private set; }

            public BinOpHelper(OpHelper next, params BinOp[] ops)
            {
                Debug.Assert(next != null);
                Next = next;
                Debug.Assert(ops.Length > 0);
                Ops = ops;
            }

            public override Expr_Num Match(ref Token token)
            {
                var left = Next.Match(ref token);
                if (left == null)
                    return null;

                bool opFound;
                do
                {
                    opFound = false;
                    foreach (var op in Ops)
                    {
                        if (MatchSymbol(ref token, op.Op, false))
                        {
                            var right = Next.Match(ref token);
                            if (right == null)
                                throw new ParserException(token, _errSubExpressionExpected);

                            left = new Expr_BinOp(op, left, right);
                            opFound = true;
                            break;
                        }
                    }
                }
                while (opFound);
                
                return left;
            }
        }

        static UnaryOpHelper _unaryOp = new UnaryOpHelper(new UnOpNeg(), new UnOpLogNeg(), new UnOpCompl());
        static BinOpHelper _binaryMulOpHelper = new BinOpHelper(_unaryOp, new BinOpMul(), new BinOpDiv(), new BinOpMod());
        static BinOpHelper _binaryAddOpHelper = new BinOpHelper(_binaryMulOpHelper, new BinOpAdd(), new BinOpSub());
        static BinOpHelper _binaryShiftOpHelper = new BinOpHelper(_binaryAddOpHelper, new BinOpShL(), new BinOpShR());
        static BinOpHelper _binaryRelOpHelper = new BinOpHelper(_binaryShiftOpHelper, new BinOpLess(), new BinOpMore(), new BinOpLessEq(), new BinOpMoreEq());
        static BinOpHelper _binaryEqOpHelper = new BinOpHelper(_binaryRelOpHelper, new BinOpEq(), new BinOpNotEq());
        static BinOpHelper _binaryAndOpHelper = new BinOpHelper(_binaryEqOpHelper, new BinOpAdd());
        static BinOpHelper _binaryXorOpHelper = new BinOpHelper(_binaryAndOpHelper, new BinOpXor());
        static BinOpHelper _binaryOrOpHelper = new BinOpHelper(_binaryXorOpHelper, new BinOpOr());
        static BinOpHelper _binaryLogAndOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogAnd());
        static BinOpHelper _binaryLogOrOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogOr());

        class Expr_UnOp : Expr_Num
        {
            public UnOp Oper { get; private set; }
            public Expr_Num Expr { get; private set; }
            public Expr_UnOp(UnOp oper, Expr_Num expr)
            {
                Oper = oper;
                Expr = Expr;
            }
        }

        class Expr_BinOp : Expr_Num
        {
            public BinOp Oper { get; private set; }
            public Expr_Num Left { get; private set; }
            public Expr_Num Right { get; private set; }
            public Expr_BinOp(BinOp oper, Expr_Num left, Expr_Num right)
            {
                Oper = oper;
                Left = left;
                Right = right;
            }
        }
        #endregion unary and binary operators

        class Expr_Ident : Expr_Num
        {
            public string Name { get; private set; }
            public Expr_Ident(string name)
            {
                Name = name;
            }
        }

        class Expr_Const : Expr_Num
        {
            public string Value { get; private set; }
            public Expr_Const(string value)
            {
                Value = value;
            }
        }

        static Expr_Num MatchPrimExpr(ref Token token)
        {
            // check for identifier
            var varName = MatchIdent(ref token, false);
            if (varName != null)
            {
                return new Expr_Ident(varName);
            }
            // check for literal
            if (token.Type == TokenType.Number)
            {
                var value = token.Value;
                token = token.Next;
                return new Expr_Const(value);
            }

            MatchSymbol(ref token, "(", true);

            var expr = MatchExprNum(ref token);

            MatchSymbol(ref token, ")", true);

            return expr;
        }

        private static Expr MatchExpr(ref Token token)
        {
            var expr = Expr_Str.Match(ref token) ??
                       (Expr)MatchExprNum(ref token);
            if (expr == null)
                throw new ParserException(token, _errExpressionExpected);
            return expr;
        }

        class NumAssignOp : BinOp
        {
            public NumAssignOp(string op) : base(op) { }
        }

        class NumAssignOpSimple : NumAssignOp
        {
            public NumAssignOpSimple() : base("=") { }
        }

        class NumAssignOpMul : NumAssignOp
        {
            public NumAssignOpMul() : base("*=") { }
        }

        class NumAssignOpDiv : NumAssignOp
        {
            public NumAssignOpDiv() : base("/=") { }
        }

        class NumAssignOpMod : NumAssignOp
        {
            public NumAssignOpMod() : base("%=") { }
        }

        class NumAssignOpAdd : NumAssignOp
        {
            public NumAssignOpAdd() : base("+=") { }
        }

        class NumAssignOpSub : NumAssignOp
        {
            public NumAssignOpSub() : base("-=") { }
        }

        class Expr_NumAssign : Expr_Num
        {
            public string VarName { get; private set; }
            public Expr_Num Expr { get; private set; }
            public static NumAssignOp[] Ops = new NumAssignOp[]
            {
                new NumAssignOpSimple(),

                new NumAssignOpMul(),
                new NumAssignOpDiv(),
                new NumAssignOpMod(),

                new NumAssignOpSub(),
                new NumAssignOpAdd(),
            };

            public Expr_NumAssign(string varName, Expr_Num expr)
            {
                VarName = varName;
                Expr = expr;
            }

            public static Expr_NumAssign Match(ref Token token)
            {
                var token_copy = token;
                var varName = MatchIdent(ref token_copy, false);
                if (varName == null)
                    return null;

                foreach (var op in Expr_NumAssign.Ops)
                {
                    if (MatchSymbol(ref token_copy, "=", false))
                    {
                        // expression expected
                        var expr = MatchExprNum(ref token_copy);

                        // use token copy
                        token = token_copy;
                        return new Expr_NumAssign(varName, expr);
                    }
                }

               return null;
            }
        }

        class Expr_Cond : Expr_Num
        {
            public Expr_Num Cond { get; private set; }
            public Expr_Num ExprTrue { get; private set; }
            public Expr_Num ExprFalse { get; private set; }
            public Expr_Cond(Expr_Num cond, Expr_Num exprTrue, Expr_Num exprFalse)
            {
                Cond = cond;
                ExprTrue = exprTrue;
                ExprFalse = exprFalse;
            }
            public static Expr_Num Match(ref Token token)
            {
                var cond = _binaryLogOrOpHelper.Match(ref token);
                if (cond == null)
                    return null;

                if (MatchSymbol(ref token, "?", false))
                {
                    Expr_Num exprTrue = Expr_Cond.Match(ref token);

                    MatchSymbol(ref token, ":", true);

                    Expr_Num exprFalse = Expr_Cond.Match(ref token);

                    return new Expr_Cond(cond, exprTrue, exprFalse);
                }
                else
                {
                    return cond;
                }
            }
        }

        static Expr_Num MatchExprNum(ref Token token)
        {
            return (Expr_Num)Expr_NumAssign.Match(ref token) ??
                    Expr_Cond.Match(ref token);
        }

        class Expr_Str : Expr
        {
            public Expr_Str Left { get; private set; }
            public string Right { get; private set; }

            public Expr_Str(Expr_Str left, string right)
            {
                Left = left;
                Right = right;
            }
            public static Expr_Str Match(ref Token token, Expr_Str left = null)
            {
                if (token == null)
                    throw new ParserException(null, _errUnexpectedEndOfFile);
                if (token.Type != TokenType.String)
                    return null;

                // store value
                var expr = new Expr_Str(left, token.Value);

                token = token.Next;
                // try next for +
                if (MatchSymbol(ref token, "+", false))
                {
                    return Match(ref token, expr);
                }
                else
                {
                    return expr;
                }
            }
        }
        #endregion expressions

        #region statements
        class Stmt
        {
        }

        private static Stmt MatchStmt(ref Token token)
        {
            var stmt = Stmt_DefVar.Match(ref token) ??
                       Stmt_Assign.Match(ref token) ??
                       Stmt_For.Match(ref token) ??
                       Stmt_If.Match(ref token) ??
                       Stmt_While.Match(ref token) ??
                       (Stmt)Stmt_Block.Match(ref token, false);
            if (stmt == null)
                throw new ParserException(token, _errStatementExpected);
            return stmt;
        }

        class Stmt_DefVar : Stmt
        {
            public string VarName { get; private set; }
            public Expr Expr { get; private set; }

            public Stmt_DefVar(string varName, Expr expr)
            {
                VarName = varName;
                Expr = expr;
            }

            public static Stmt_DefVar Match(ref Token token)
            {
                if (!MatchKeyword(ref token, "var"))
                    return null;

                var varName = MatchIdent(ref token, false);
                if (varName == null)
                    return null;

                // match =
                MatchSymbol(ref token, "=", true);


                // insist for identifier
                var expr = MatchExpr(ref token);

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_DefVar(varName, expr);
            }
        }

        class Stmt_Assign : Stmt
        {
            public Expr_NumAssign Expr { get; private set; }

            public Stmt_Assign(Expr_NumAssign expr)
            {
                Expr = expr;
            }

            public static Stmt_Assign Match(ref Token token)
            {
                var expr = Expr_NumAssign.Match(ref token);
                if (expr == null)
                    return null;

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_Assign(expr);
            }
        }

        class Stmt_For : Stmt
        {
            public Expr Expr1 { get; private set; }
            public Expr Expr2 { get; private set; }
            public Expr Expr3 { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_For(Expr expr1, Expr expr2, Expr expr3, Stmt stmt)
            {
                Expr1 = expr1;
                Expr2 = expr2;
                Expr3 = expr3;
                Stmt = stmt;
            }

            public static Stmt_For Match(ref Token token)
            {
                if (!MatchKeyword(ref token, "for"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match 1st expr
                var expr1 = MatchExpr(ref token);

                // match ;
                MatchSymbol(ref token, ";", true);

                // match 2nd expr
                var expr2 = MatchExpr(ref token);

                // match ;
                MatchSymbol(ref token, ";", true);

                // match 3rd expr
                var expr3 = MatchExpr(ref token);

                // match )
                MatchSymbol(ref token, ")", true);
                
                // match statement
                var stmt = MatchStmt(ref token);

                return new Stmt_For(expr1, expr2, expr3, stmt);
            }
        }

        class Stmt_If : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt StmtThen { get; private set; }
            public Stmt StmtElse { get; private set; }

            public Stmt_If(Expr expr, Stmt stmtThen, Stmt stmtElse)
            {
                Expr = expr;
                StmtThen = stmtThen;
                StmtElse = stmtElse;
            }

            public static Stmt_If Match(ref Token token)
            {
                if (!MatchKeyword(ref token, "if"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match cond expr
                var expr = MatchExpr(ref token);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmtThen = MatchStmt(ref token);

                Stmt stmtElse = null;
                if (MatchKeyword(ref token, "else"))
                {

                    // match statement
                    stmtElse = MatchStmt(ref token);
                }
                return new Stmt_If(expr, stmtThen, stmtElse);
            }
        }

        class Stmt_While : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_While(Expr expr, Stmt stmt)
            {
                Expr = expr;
                Stmt = stmt;
            }

            public static Stmt_While Match(ref Token token)
            {
                if (!MatchKeyword(ref token, "while"))
                    return null;

                // match (
                MatchSymbol(ref token, "(", true);

                // match cond expr
                var expr = MatchExpr(ref token);

                // match )
                MatchSymbol(ref token, ")", true);

                // match statement
                var stmt = MatchStmt(ref token);

                return new Stmt_While(expr, stmt);
            }
        }

        class Stmt_Block : Stmt
        {
            public readonly List<Stmt> Stmts = new List<Stmt>();
            public static Stmt_Block Match(ref Token token, bool insist)
            {
                if (!MatchSymbol(ref token, "{", insist))
                    return null;

                var block = new Stmt_Block();
                for (; ; )
                {
                    // check for )
                    if (MatchSymbol(ref token, "}", false))
                        break;

                    // consume statement
                    var stmt = MatchStmt(ref token);
                    block.Stmts.Add(stmt);
                }
                return block;
            }
        }

        class Stmt_SubCall : Stmt
        {
            public string SubName { get; set; }
            public readonly List<Expr> ParamValues = new List<Expr>();
        }
        #endregion statements

        #region primitives
        private static string MatchIdent(ref Token token, bool insist)
        {
            if (token == null)
                throw new ParserException(null, _errUnexpectedEndOfFile);
            if (token.Type == TokenType.Identifier)
            {
                var value = token.Value;
                token = token.Next;
                return value;
            }
            if (insist)
                throw new ParserException(token, _errIdentifierExpected);
            else 
                return null;
        }

        private static bool MatchSymbol(ref Token token, string symbol, bool insist)
        {
            if (token == null)
                throw new ParserException(null, _errUnexpectedEndOfFile);
            if (token.Type == TokenType.Symbol && token.Value == symbol)
            {
                token = token.Next;
                return true;
            }

            if (insist)
                throw new ParserException(token, string.Format(_errfmt_0_Expected, symbol));
            else
                return false;
        }

        private static bool MatchKeyword(ref Token token, string keyword)
        {
            if (token == null)
                throw new ParserException(null, _errUnexpectedEndOfFile);
            if (token.Type == TokenType.Keyword && token.Value == keyword)
            {
                token = token.Next;
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion primitives

        class Sub
        {
            public string SubName { get; set; }
            public readonly List<string> Params = new List<string>();
            public Stmt_Block Block { get; set; }

            public static Sub Match(ref Token token)
            {
                // should start with sub
                if (!MatchKeyword(ref token, "sub"))
                    return null;

                // insist for sub name
                var subName = MatchIdent(ref token, true);
                var sub = new Sub() { SubName = subName };

                // insist for (
                MatchSymbol(ref token, "(", true);

                bool nextParam = false;
                for (; ; )
                {
                    // check for )
                    if (MatchSymbol(ref token, ")", false))
                        break;

                    if (nextParam)
                    {
                        // check for ,
                        MatchSymbol(ref token, ",", true);
                    }

                    // get param name
                    var param = MatchIdent(ref token, true);
                    sub.Params.Add(param);
                }

                // block should follow
                sub.Block = Stmt_Block.Match(ref token, true);
                return sub;
            }
        }

        class Script
        {
            public readonly List<Stmt> Statements = new List<Stmt>();
            public readonly List<Sub> Subroutines = new List<Sub>();

            public static Script Parse(Token token)
            {
                var script = new Script();
                while (token != null)
                {
                    var stmt = MatchStmt(ref token);
                    if (stmt != null)
                    {
                        script.Statements.Add(stmt);
                        continue;
                    }
                    var sub = Sub.Match(ref token);
                    if (sub != null)
                    {
                        script.Subroutines.Add(sub);
                        continue;
                    }
                }

                return script;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                string[] lines = File.ReadAllLines("script.txt");
                var token = Tokenize(lines);
                var script = Script.Parse(token);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.ReadLine();
        }
    }
}
