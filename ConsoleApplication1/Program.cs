﻿using ConsoleApplication1.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            Double,
            Integer,
            HexNumber,
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
                                            @"(?'hex'(?i:0x[\da-f]+))|" +
                                            @"(?'double'(?:\d*\.)?\d+[eE][-+]?\d+|\d*\.\d+)|" +
                                            @"(?'int'\d+)|" +
                                            @"(?'kwd'sub|var|for|if|else|while)|" +
                                            @"(?'id'[\w_][\w\d_]*)|" +
                                            @"(?'sym'[-+*/%&^|<>!=]=|&&|\|\||<<|>>|[-+~/*%&^|?:=(){};,<>]))", RegexOptions.Compiled);

        static Token Tokenize(string[] lines)
        {
            Token first = null;
            Token last = null;
            if (lines != null && lines.Length > 0)
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
                            else if (groups["double"].Success)
                                token = new Token(TokenType.Double, match.Value, line, match.Index);
                            else if (groups["int"].Success)
                                token = new Token(TokenType.Integer, match.Value, line, match.Index);
                            else if (groups["hex"].Success)
                                token = new Token(TokenType.HexNumber, match.Value, line, match.Index);
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
                base(token == null ? message : string.Format("({0},{1}): {2}", token.Line + 1, token.Column + 1, message)) { }
        }
        class ExecutionException : ApplicationException
        {
            public ExecutionException(Token token, string message) :
                base(token == null ? message : string.Format("({0},{1}): {2}", token.Line + 1, token.Column + 1, message)) { }
        }

        const string _errUnexpectedEndOfFile = "unexpected end of file";
        const string _errIdentifierExpected = "identifier expected";
        const string _errStatementExpected = "statement expected";
        const string _errExpressionExpected = "expression expected";
        const string _errAssignmentOperatorExpected = "assignment operator expected";
        const string _errSubExpressionExpected = "sub-expression expected";
        const string _errfmt_0_Expected = "{0} expected";
        const string _errfmtVariable_0_AlreadyDefined = "variable {0} already defined";
        const string _errfmtVariable_0_NotDefined = "variable {0} not defined";
        const string _errfmtFailedToApply_0_operator = "failed to apply {0} operator";
        const string _errBooleanValueExpected = "boolean value expected";
        #endregion errors

        #region expressions

        abstract class Expr : Stmt
        {
            public Expr(Token startToken) : base(startToken) { }

            public override void Execute(Context context)
            {
                Calculate(context);
            }
            public abstract Value Calculate(Context context);
        }

        #region unary and binary operators


        class BaseOp
        {
            public Token StartToken { get; set; }
            public string OpName { get; private set; }
            public BaseOp(string opName) { OpName = opName; }

            protected Value CheckValueNotNull(Value value)
            {
                if (value == null)
                    throw new ExecutionException(StartToken, string.Format(_errfmtFailedToApply_0_operator, OpName));
                return value;
            }
        }

        abstract class BinOp : BaseOp
        {
            public BinOp(string opName) : base(opName) { }
            public abstract Value Apply(Context context, Expr Left, Expr Right);
        }

        class BinOpDelegates : BinOp
        {
            public Func<ValueDouble, ValueDouble, Value> OpDouble { get; private set; }
            public Func<ValueInt, ValueInt, Value> OpInt { get; private set; }
            public Func<bool, bool, bool> OpBool { get; private set; }
            public Func<ValueString, ValueString, Value> OpString { get; private set; }

            public BinOpDelegates(string opName,
                         Func<ValueDouble, ValueDouble, Value> opDouble,
                         Func<ValueInt, ValueInt, Value> opInt,
                         Func<bool, bool, bool> opBool = null,
                         Func<ValueString, ValueString, Value> opString = null)
                : base(opName)
            {
                OpDouble = opDouble;
                OpInt = opInt;
                OpBool = opBool;
                OpString = opString;
            }

            public BinOp Clone() { return (BinOp)MemberwiseClone(); }

            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                return Value.ApplyBinOp(StartToken, this, Left.Calculate(context), Right.Calculate(context));
            }
        }

        class BinOpLogAnd : BinOp
        {
            public BinOpLogAnd() : base("&&") { }

            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                if (Left.Calculate(context).AsBool)
                {
                    return ValueBool.Box(Right.Calculate(context).AsBool);
                }
                else
                {
                    return ValueBool.False;
                }
            }
        }

        class BinOpLogOr : BinOp
        {
            public BinOpLogOr() : base("||") { }
            public override Value Apply(Context context, Expr Left, Expr Right)
            {
                if (Left.Calculate(context).AsBool)
                {
                    return ValueBool.True;
                }
                else
                {
                    return ValueBool.Box(Right.Calculate(context).AsBool);
                }
            }
        }

        abstract class UnOp : BaseOp
        {
            public UnOp(string opName) : base(opName) { }

            public UnOp Clone() { return (UnOp)MemberwiseClone(); }

            public abstract Value Apply(Value innerValue);
        }

        class UnOpLogNeg : UnOp
        {
            public UnOpLogNeg() : base("!") { }
            public override Value Apply(Value innerValue)
            {
                return ValueBool.Box(!innerValue.AsBool);
            }
        }

        class UnOpNeg : UnOp
        {
            public UnOpNeg() : base("-") { }
            public override Value Apply(Value innerValue)
            {
                var valueInt = innerValue as ValueInt;
                if (valueInt != null)
                    return new ValueInt(-valueInt.Value);
                var valueDouble = innerValue as ValueDouble;
                if (valueDouble != null)
                    return new ValueDouble(-valueDouble.Value);
                return CheckValueNotNull(null);
            }
        }

        class UnOpCompl : UnOp
        {
            public UnOpCompl() : base("~") { }
            public override Value Apply(Value innerValue)
            {
                var valueInt = innerValue as ValueInt;
                if (valueInt != null)
                    return new ValueInt(~valueInt.Value);
                return CheckValueNotNull(null);
            }
        }

        abstract class OpHelper
        {
            public abstract Expr Match(ref Token token);
        }

        class UnaryOpHelper : OpHelper
        {
            public UnOp[] Ops { get; private set; }

            public UnaryOpHelper(params UnOp[] ops)
            {
                Debug.Assert(ops.Length > 0);
                Ops = ops;
            }

            public override Expr Match(ref Token token)
            {
                var startToken = token;
                foreach (var op in Ops)
                {
                    if (MatchSymbol(ref token, op.OpName, false))
                    {
                        var expr = Match(ref token);
                        if (expr == null)
                            throw new ParserException(token, _errSubExpressionExpected);

                        var opCopy = op.Clone();
                        opCopy.StartToken = startToken;
                        return new Expr_UnOp(startToken, op, expr);
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

            public override Expr Match(ref Token token)
            {
                var startToken = token;
                var left = Next.Match(ref token);
                if (left == null)
                    return null;

                bool opFound;
                do
                {
                    opFound = false;
                    foreach (var op in Ops)
                    {
                        var symbolToken = token;
                        if (MatchSymbol(ref token, op.OpName, false))
                        {
                            var right = Next.Match(ref token);
                            if (right == null)
                                throw new ParserException(token, _errSubExpressionExpected);

                            var opCopy = op;
                            opCopy.StartToken = symbolToken;
                            left = new Expr_BinOp(startToken, opCopy, left, right);
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
        static BinOpHelper _binaryMulOpHelper = new BinOpHelper(_unaryOp,
            new BinOpDelegates("*", (ValueDouble l, ValueDouble r) => new ValueDouble(l.Value * r.Value), (ValueInt l, ValueInt r) => new ValueInt(l.Value * r.Value)),
            new BinOpDelegates("/", (ValueDouble l, ValueDouble r) => new ValueDouble(l.Value / r.Value), (ValueInt l, ValueInt r) => new ValueInt(l.Value / r.Value)),
            new BinOpDelegates("%", (ValueDouble l, ValueDouble r) => new ValueDouble(l.Value % r.Value), (ValueInt l, ValueInt r) => new ValueInt(l.Value % r.Value)));
        static BinOpHelper _binaryAddOpHelper = new BinOpHelper(_binaryMulOpHelper,
            new BinOpDelegates("+", (ValueDouble l, ValueDouble r) => new ValueDouble(l.Value + r.Value), (ValueInt l, ValueInt r) => new ValueInt(l.Value + r.Value), null , (ValueString l, ValueString r) => new ValueString(l.Value + r.Value)),
            new BinOpDelegates("-", (ValueDouble l, ValueDouble r) => new ValueDouble(l.Value - r.Value), (ValueInt l, ValueInt r) => new ValueInt(l.Value - r.Value)));
        static BinOpHelper _binaryShiftOpHelper = new BinOpHelper(_binaryAddOpHelper,
            new BinOpDelegates("<<", null, (ValueInt l, ValueInt r) => new ValueInt(l.Value << r.Value)),
            new BinOpDelegates(">>", null, (ValueInt l, ValueInt r) => new ValueInt(l.Value >> r.Value)));
        static BinOpHelper _binaryRelOpHelper = new BinOpHelper(_binaryShiftOpHelper,
            new BinOpDelegates("<", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value < r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value < r.Value)),
            new BinOpDelegates(">", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value > r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value > r.Value)),
            new BinOpDelegates("<=", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value <= r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value <= r.Value)),
            new BinOpDelegates(">=", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value >= r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value >= r.Value)));
        static BinOpHelper _binaryEqOpHelper = new BinOpHelper(_binaryRelOpHelper,
            new BinOpDelegates("==", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value == r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value == r.Value), (bool l, bool r) => l == r, (ValueString l, ValueString r) => new ValueBool(l.Value == r.Value)),
            new BinOpDelegates("!=", (ValueDouble l, ValueDouble r) => new ValueBool(l.Value != r.Value), (ValueInt l, ValueInt r) => new ValueBool(l.Value != r.Value), (bool l, bool r) => l != r, (ValueString l, ValueString r) => new ValueBool(l.Value != r.Value)));
        static BinOpHelper _binaryAndOpHelper = new BinOpHelper(_binaryEqOpHelper,
            new BinOpDelegates("&", null, (ValueInt l, ValueInt r) => new ValueInt(l.Value & r.Value), (bool l, bool r) => l & r));
        static BinOpHelper _binaryXorOpHelper = new BinOpHelper(_binaryAndOpHelper,
            new BinOpDelegates("^", null, (ValueInt l, ValueInt r) => new ValueInt(l.Value ^ r.Value), (bool l, bool r) => l ^ r));
        static BinOpHelper _binaryOrOpHelper = new BinOpHelper(_binaryXorOpHelper,
            new BinOpDelegates("|", null, (ValueInt l, ValueInt r) => new ValueInt(l.Value | r.Value), (bool l, bool r) => l | r));
        static BinOpHelper _binaryLogAndOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogAnd());
        static BinOpHelper _binaryLogOrOpHelper = new BinOpHelper(_binaryOrOpHelper, new BinOpLogOr());

        class Expr_UnOp : Expr
        {
            public UnOp Oper { get; private set; }
            public Expr Expr { get; private set; }
            public Expr_UnOp(Token startToken, UnOp oper, Expr expr)
                : base(startToken)
            {
                Oper = oper;
                Expr = Expr;
            }

            public override Value Calculate(Context context)
            {
                Value innerValue = Expr.Calculate(context);
                return Oper.Apply(innerValue);
            }
        }

        class Expr_BinOp : Expr
        {
            public BinOp Oper { get; private set; }
            public Expr Left { get; private set; }
            public Expr Right { get; private set; }
            public Expr_BinOp(Token startToken, BinOp oper, Expr left, Expr right)
                : base(startToken)
            {
                Oper = oper;
                Left = left;
                Right = right;
            }

            public override Value Calculate(Context context)
            {
                return Oper.Apply(context, Left, Right);
            }
        }
        #endregion unary and binary operators

        class Expr_Ident : Expr
        {
            public string Name { get; private set; }
            public Expr_Ident(Token startToken, string name)
                : base(startToken)
            {
                Name = name;
            }

            public override Value Calculate(Context context)
            {
                var var = context.GetVariable(Name, true);
                if (var == null)
                    throw new ExecutionException(StartToken, string.Format(_errfmtVariable_0_NotDefined, Name));

                return var.Value;
            }
        }

        class Expr_Const : Expr
        {
            public Value Value { get; private set; }
            public Expr_Const(Token startToken, Value value)
                : base(startToken)
            {
                Value = value;
            }

            public override Value Calculate(Context context) { return Value; }
        }

        #region assignment
        class Expr_Assign : Expr
        {
            public string VarName { get; private set; }
            public Expr Expr { get; private set; }

            public Expr_Assign(Token startToken, string varName, Expr expr)
                : base(startToken)
            {
                VarName = varName;
                Expr = expr;
            }

            public static Expr_Assign Match(ref Token token)
            {
                var startToken = token;
                var token_copy = token;
                var varName = MatchIdent(ref token_copy, false);
                if (varName == null)
                    return null;

                if (MatchSymbol(ref token_copy, "=", false))
                {
                    // expression expected
                    var expr = MatchExpr(ref token_copy);

                    // use token copy
                    token = token_copy;
                    return new Expr_Assign(startToken, varName, expr);
                }

                return null;
            }

            public override Value Calculate(Context context)
            {
                var var = context.GetVariable(VarName, true);
                if (var == null)
                    throw new ExecutionException(StartToken, string.Format(_errfmtVariable_0_NotDefined, VarName));

                return var.Value = Expr.Calculate(context);
            }
        }
        #endregion assignment

        #region condition
        class Expr_Cond : Expr
        {
            public Expr Cond { get; private set; }
            public Expr ExprTrue { get; private set; }
            public Expr ExprFalse { get; private set; }
            public Expr_Cond(Token startToken, Expr cond, Expr exprTrue, Expr exprFalse)
                : base(startToken)
            {
                Cond = cond;
                ExprTrue = exprTrue;
                ExprFalse = exprFalse;
            }
            public static Expr Match(ref Token token)
            {
                var startToken = token;
                var cond = _binaryLogOrOpHelper.Match(ref token);
                if (cond == null)
                    return null;

                if (MatchSymbol(ref token, "?", false))
                {
                    var exprTrue = Expr_Cond.Match(ref token);

                    MatchSymbol(ref token, ":", true);

                    var exprFalse = Expr_Cond.Match(ref token);

                    return new Expr_Cond(startToken, cond, exprTrue, exprFalse);
                }
                else
                {
                    return cond;
                }
            }
            public override Value Calculate(Context context)
            {
                var cond = Cond.Calculate(context);
                if (cond.AsBool)
                {
                    return ExprTrue.Calculate(context);
                }
                else
                {
                    return ExprFalse.Calculate(context);
                }
            }
        }
        #endregion condition

        static Expr MatchPrimExpr(ref Token token)
        {
            var startToken = token;
            // check for identifier
            var varName = MatchIdent(ref token, false);
            if (varName != null)
            {
                return new Expr_Ident(startToken, varName);
            }
            // check for string literal
            if (token.Type == TokenType.String)
            {
                startToken = token;
                string sValue = token.Value;
                token = token.Next;
                return new Expr_Const(startToken, new ValueString(sValue));
            }
            // check for integer literal
            if (token.Type == TokenType.Double)
            {
                startToken = token;
                double dValue = double.Parse(token.Value, CultureInfo.InvariantCulture);
                token = token.Next;
                return new Expr_Const(startToken, new ValueDouble(dValue));
            }
            // check for int literal
            if (token.Type == TokenType.Integer)
            {
                startToken = token;
                int iValue = int.Parse(token.Value);
                token = token.Next;
                return new Expr_Const(startToken, new ValueInt(iValue));
            }
            // check for hex literal
            if (token.Type == TokenType.HexNumber)
            {
                startToken = token;
                int iValue = int.Parse(token.Value.Substring(2), NumberStyles.HexNumber);
                token = token.Next;
                return new Expr_Const(startToken, new ValueInt(iValue));
            }

            MatchSymbol(ref token, "(", true);

            var expr = MatchExpr(ref token);

            MatchSymbol(ref token, ")", true);

            return expr;
        }

        private static Expr MatchExpr(ref Token token)
        {
            var expr = (Expr)Expr_Assign.Match(ref token) ??
                        Expr_Cond.Match(ref token);
            if (expr == null)
                throw new ParserException(token, _errExpressionExpected);
            return expr;
        }
        #endregion expressions

        abstract class Value
        {
            public static Value ApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                var value = ValueInt.TryApplyBinOp(startToken, binOp, value1, value2) ??
                            ValueDouble.TryApplyBinOp(startToken, binOp, value1, value2) ??
                            ValueString.TryApplyBinOp(startToken, binOp, value1, value2) ??
                            ValueBool.TryApplyBinOp(startToken, binOp, value1, value2);
                if (value == null)
                    throw new ExecutionException(startToken, string.Format(_errfmtFailedToApply_0_operator, binOp.OpName));
                return value;
            }

            public abstract bool AsBool { get; }
        }

        class ValueBool : Value
        {
            public readonly static ValueBool True = new ValueBool(true);
            public readonly static ValueBool False = new ValueBool(false);

            public static ValueBool Box(bool value) { return value ? True : False; }

            public bool Value { get; private set; }
            public ValueBool(bool value)
            {
                Value = value;
            }

            public static Value TryApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpBool == null)
                    return null;
                bool b1 = value1.AsBool;
                bool b2 = value2.AsBool;

                return ValueBool.Box(binOp.OpBool(b1, b2));
            }

            public override bool AsBool { get { return Value; } }
        }

        class ValueInt : Value
        {
            public int Value { get; private set; }
            public ValueInt(int value)
            {
                Value = value;
            }
            public static Value TryApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpInt == null)
                    return null;

                var i1 = value1 as ValueInt;
                var i2 = value2 as ValueInt;
                if (i1 == null || i2 == null)
                    return null;

                return binOp.OpInt(i1, i2);
            }

            public override bool AsBool { get { return Value != 0; } }
        }

        class ValueDouble : Value
        {
            public double Value { get; private set; }
            public ValueDouble(double value)
            {
                Value = value;
            }
            public static Value TryApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpDouble == null)
                    return null;

                var d1 = value1 as ValueDouble;
                var d2 = value2 as ValueDouble;
                if (d1 == null)
                {
                    var i1 = value1 as ValueInt;
                    if (i1 != null)
                        d1 = new ValueDouble(i1.Value);
                    return null;
                }
                if (d2 == null)
                {
                    var i2 = value2 as ValueInt;
                    if (i2 != null)
                        d2 = new ValueDouble(i2.Value);
                    return null;
                }
                if (d1 == null || d2 == null)
                    return null;

                return binOp.OpDouble(d1, d2);
            }

            public override bool AsBool { get { return Value != 0; } }
        }

        class ValueString : Value
        {
            public string Value { get; private set; }
            public ValueString(string value)
            {
                Value = value;
            }
            public static Value TryApplyBinOp(Token startToken, BinOpDelegates binOp, Value value1, Value value2)
            {
                if (binOp.OpString == null)
                    return null;

                var d1 = value1 as ValueString;
                var d2 = value2 as ValueString;
                if (d1 == null || d2 == null)
                    return null;

                return binOp.OpString(d1, d2);
            }

            public override bool AsBool { get { return !string.IsNullOrEmpty(Value); } }
        }

        class Variable
        {
            public string Name { get; private set; }

            public Value Value { get; set; }

            public Variable(string name)
            {
                Name = name;
            }
        }

        class Context
        {
            public Context Parent { get; private set; }
            public List<Variable> Variables { get; private set; }

            public Context(Context parent)
            {
                Parent = parent;
                Variables = new List<Variable>();
            }

            public Variable GetVariable(string name, bool searchParents)
            {
                return Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)) ??
                        (searchParents && Parent != null ? Parent.GetVariable(name, true) : null);
            }

            public Variable AddVariable(Token token, string name)
            {
                var var = GetVariable(name, false);
                if (var != null)
                    throw new ExecutionException(token, string.Format(_errfmtVariable_0_AlreadyDefined, name));

                var = new Variable(name);
                Variables.Add(var);
                return var;
            }
        }

        #region statements
        abstract class Stmt
        {
            public Token StartToken { get; private set; }
            public abstract void Execute(Context context);

            public Stmt(Token startToken)
            {
                StartToken = startToken;
            }

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

            public Stmt_DefVar(Token startToken, string varName, Expr expr)
                : base(startToken)
            {
                VarName = varName;
                Expr = expr;
            }

            public static Stmt_DefVar Match(ref Token token)
            {
                var startToken = token;
                if (!MatchKeyword(ref token, "var"))
                    return null;

                // check for identifier
                var varName = MatchIdent(ref token, true);

                Expr expr = null;
                // check for =
                if (MatchSymbol(ref token, "=", false))
                {

                    // consume assignment expression
                    expr = Expr_Assign.Match(ref token);
                }

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_DefVar(startToken, varName, expr);
            }

            public override void Execute(Context context)
            {
                var var = context.AddVariable(StartToken, VarName);
                if (Expr != null)
                    var.Value = Expr.Calculate(context);
            }
        }

        class Stmt_Assign : Stmt
        {
            public Expr_Assign Expr { get; private set; }

            public Stmt_Assign(Token startToken, Expr_Assign expr)
                : base(startToken)
            {
                Expr = expr;
            }

            public static Stmt_Assign Match(ref Token token)
            {
                var startToken = token;
                var expr = Expr_Assign.Match(ref token);
                if (expr == null)
                    return null;

                // match ;
                MatchSymbol(ref token, ";", true);

                return new Stmt_Assign(startToken, expr);
            }

            public override void Execute(Context context)
            {
                Expr.Execute(context);
            }
        }

        class Stmt_For : Stmt
        {
            public Expr Expr1 { get; private set; }
            public Expr Expr2 { get; private set; }
            public Expr Expr3 { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_For(Token startToken, Expr expr1, Expr expr2, Expr expr3, Stmt stmt)
                : base(startToken)
            {
                Expr1 = expr1;
                Expr2 = expr2;
                Expr3 = expr3;
                Stmt = stmt;
            }

            public static Stmt_For Match(ref Token token)
            {
                var startToken = token;
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

                return new Stmt_For(startToken, expr1, expr2, expr3, stmt);
            }

            public override void Execute(Context context)
            {
                for (Expr1.Calculate(context); Expr2.Calculate(context).AsBool; Expr3.Calculate(context))
                    Stmt.Execute(context);
            }
        }

        class Stmt_If : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt StmtThen { get; private set; }
            public Stmt StmtElse { get; private set; }

            public Stmt_If(Token startToken, Expr expr, Stmt stmtThen, Stmt stmtElse)
                : base(startToken)
            {
                Expr = expr;
                StmtThen = stmtThen;
                StmtElse = stmtElse;
            }

            public static Stmt_If Match(ref Token token)
            {
                var startToken = token;
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
                return new Stmt_If(startToken, expr, stmtThen, stmtElse);
            }

            public override void Execute(Context context)
            {
                if (Expr.Calculate(context).AsBool)
                {
                    StmtThen.Execute(context);
                }
                else
                {
                    if (StmtElse != null)
                        StmtElse.Execute(context);
                }
            }
        }

        class Stmt_While : Stmt
        {
            public Expr Expr { get; private set; }
            public Stmt Stmt { get; private set; }

            public Stmt_While(Token startToken, Expr expr, Stmt stmt)
                : base(startToken)
            {
                Expr = expr;
                Stmt = stmt;
            }

            public static Stmt_While Match(ref Token token)
            {
                var startToken = token;
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

                return new Stmt_While(startToken, expr, stmt);
            }

            public override void Execute(Context context)
            {
                while (Expr.Calculate(context).AsBool)
                {
                    Stmt.Execute(context);
                }
            }
        }

        class Stmt_Block : Stmt
        {
            public readonly List<Stmt> Stmts = new List<Stmt>();

            public Stmt_Block(Token startToken) : base(startToken) { }
            public static Stmt_Block Match(ref Token token, bool insist)
            {
                var startToken = token;
                if (!MatchSymbol(ref token, "{", insist))
                    return null;

                var block = new Stmt_Block(startToken);
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

            public override void Execute(Context context)
            {
                var innerContext = new Context(context);
                foreach (var stmt in Stmts)
                {
                    stmt.Execute(innerContext);
                }
            }
        }

        class Stmt_SubCall : Stmt
        {
            public string SubName { get; set; }
            public List<Expr> ParamValues { get; private set; }

            public Stmt_SubCall(Token startToken, string subName)
                : base(startToken)
            {
                SubName = subName;

                ParamValues = new List<Expr>();
            }

            public override void Execute(Context context)
            {
                throw new NotImplementedException();
            }
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

        class Script : Stmt_Block
        {
            public readonly List<Sub> Subs = new List<Sub>();

            public Script() : base(null) { }

            public static Script Parse(Token token)
            {
                var script = new Script();
                while (token != null)
                {
                    var stmt = MatchStmt(ref token);
                    if (stmt != null)
                    {
                        script.Stmts.Add(stmt);
                        continue;
                    }
                    var sub = Sub.Match(ref token);
                    if (sub != null)
                    {
                        script.Subs.Add(sub);
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
                string[] lines = File.ReadAllLines("example1.txt");
                var token = Tokenize(lines);
                var script = Script.Parse(token);
                var context = new Context(null);
                var varX = context.AddVariable(null, "x");
                varX.Value = new ValueString("o");
                var varY = context.AddVariable(null, "y");
                varY.Value = new ValueString("O");
                script.Execute(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            Console.ReadLine();
        }
    }
}
