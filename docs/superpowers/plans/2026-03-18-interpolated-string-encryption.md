# Interpolated String Encryption Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `StringEncryptionTransform` to encrypt the literal text portions of C# interpolated strings (`$"..."`) instead of leaving them as plaintext in the obfuscated binary.

**Architecture:** Override `VisitInterpolatedStringExpression` in the existing `CSharpSyntaxRewriter` subclass. Each `InterpolatedStringTextSyntax` node (the literal text between `{}` holes) is replaced with an `InterpolationSyntax` hole containing a `CreateDecryptorCall(...)` invocation. This converts `$"Hello {x} world"` into `$"{_Ns._Dec._D(bytes, key)}{x}{_Ns._Dec._D(bytes2, key2)}"`, preserving runtime semantics while hiding the plaintext. All existing exclusion rules (attributes, `const`, switch labels, patterns) apply to the enclosing interpolated string too.

**Tech Stack:** C# 12, Roslyn (`Microsoft.CodeAnalysis.CSharp`), MSTest

---

## File Map

| File | Change |
|------|--------|
| `Payload_Type/athena/athena/agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs` | Add `VisitInterpolatedStringExpression` override; remove TODO comment |
| `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/StringEncryptionTests.cs` | Add 3 new test methods for interpolated strings |

---

## Task 1: Write the failing tests for interpolated string encryption

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/StringEncryptionTests.cs:94`

- [ ] **Step 1: Add three test methods at the end of `StringEncryptionTests.cs` (after line 93, before the closing `}`)**

  The `ApplyTransform` helper already exists in that file (uses `StringEncryptionTransform` with class `_Dec`, method `_D`, namespace `_Ns`, default seed 42). Add exactly these methods:

  ```csharp
  [TestMethod]
  public void InterpolatedString_LiteralParts_AreReplaced()
  {
      // The literal portions "Hello " and " world" must be encrypted;
      // the interpolation hole {x} must be preserved.
      var source =
          "class C { void M(string x) {"
          + " var s = $\"Hello {x} world\"; } }";
      var result = ApplyTransform(source);
      Assert.IsFalse(result.Contains("\"Hello \""),
          "literal 'Hello ' should not appear as plaintext");
      Assert.IsFalse(result.Contains("\" world\""),
          "literal ' world' should not appear as plaintext");
      Assert.IsTrue(result.Contains("new byte[]"),
          "encrypted byte arrays should be present");
  }

  [TestMethod]
  public void InterpolatedString_ExpressionHoles_ArePreserved()
  {
      // The {x} hole expression must survive unchanged.
      var source =
          "class C { void M(string x) {"
          + " var s = $\"prefix {x}\"; } }";
      var result = ApplyTransform(source);
      // The identifier x must still appear as an interpolation hole
      Assert.IsTrue(result.Contains("{x}"),
          "interpolation hole {x} must be preserved verbatim");
  }

  [TestMethod]
  public void InterpolatedString_InsideAttribute_IsNotReplaced()
  {
      // Strings in attributes must never be touched, including
      // text portions of interpolated strings used as default values etc.
      // We test a field initialiser that is NOT an attribute to confirm
      // the attribute-exclusion path is separate from normal paths.
      // (The attribute exclusion for interpolated strings is verified
      // indirectly: an interpolated string can't appear as an attribute
      // argument in standard C#, so we verify const exclusion instead.)
      var source =
          "class C { const string X = $\"constant\"; }";
      var result = ApplyTransform(source);
      // const interpolated string: text must not be replaced
      Assert.IsTrue(result.Contains("constant"),
          "const interpolated string text must not be encrypted");
  }
  ```

- [ ] **Step 2: Run the new tests to confirm they fail**

  ```
  cd Payload_Type/athena/athena/agent_code
  dotnet test Tests/Obfuscator.Tests/ --filter "InterpolatedString" -v normal
  ```

  Expected: all 3 new tests **FAIL** (2 with `Assert.IsFalse` because the literal text IS still present, 1 with `Assert.IsTrue` — actually `InterpolatedString_InsideAttribute_IsNotReplaced` may already pass since const exclusion already works for regular literals; verify it fails or passes and note which).

- [ ] **Step 3: Commit the failing tests**

  ```
  git add Payload_Type/athena/athena/agent_code/Tests/Obfuscator.Tests/StringEncryptionTests.cs
  git commit -m "test: add failing tests for interpolated string encryption"
  ```

---

## Task 2: Implement `VisitInterpolatedStringExpression`

**Files:**
- Modify: `Payload_Type/athena/athena/agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs:69-71`

- [ ] **Step 1: Replace the TODO comment block (lines 69-71) with the full implementation**

  Remove these lines:
  ```csharp
  // TODO: Handle InterpolatedStringExpression by encrypting
  // InterpolatedStringText portions and rebuilding via
  // string.Concat. Skipped for initial implementation.
  ```

  Replace with:
  ```csharp
  public override SyntaxNode? VisitInterpolatedStringExpression(
      InterpolatedStringExpressionSyntax node)
  {
      // Apply the same exclusion rules as regular string literals
      if (IsInsideAttribute(node))
          return base.VisitInterpolatedStringExpression(node);
      if (IsConstDeclaration(node))
          return base.VisitInterpolatedStringExpression(node);
      if (IsInsideSwitchLabel(node))
          return base.VisitInterpolatedStringExpression(node);
      if (IsInsidePattern(node))
          return base.VisitInterpolatedStringExpression(node);

      // Visit children first so nested string literals in holes are
      // encrypted, then walk our own contents to encrypt text spans.
      var visited = (InterpolatedStringExpressionSyntax)
          base.VisitInterpolatedStringExpression(node)!;

      var newContents =
          new List<InterpolatedStringContentSyntax>(
              visited.Contents.Count);
      bool changed = false;

      foreach (var content in visited.Contents)
      {
          if (content is InterpolatedStringTextSyntax text)
          {
              // ValueText is the unescaped text value
              var rawText = text.TextToken.ValueText;
              if (rawText.Length == 0)
              {
                  newContents.Add(content);
                  continue;
              }

              // Encrypt and wrap in an interpolation hole so the
              // decryptor call is evaluated at runtime:
              // $"Hello {x}" → $"{_Ns._Dec._D(bytes, key)}{x}"
              var decryptorCall = CreateDecryptorCall(rawText, content);
              newContents.Add(
                  Interpolation(decryptorCall));
              changed = true;
          }
          else
          {
              newContents.Add(content);
          }
      }

      return changed
          ? visited.WithContents(List(newContents))
          : visited;
  }
  ```

  **Important note on `using` directives**: `List<T>` and `Interpolation` are already covered by existing usings (`System.Collections.Generic` is implicitly available in net8+ global usings; `Interpolation` is in `Microsoft.CodeAnalysis.CSharp.SyntaxFactory` which is already `using static`'d at line 5). No new `using` statements are needed.

- [ ] **Step 2: Build to confirm no compilation errors**

  ```
  cd Payload_Type/athena/athena/agent_code
  dotnet build Obfuscator/Obfuscator.csproj -q
  ```

  Expected: `Build succeeded.` with 0 warnings (or same count as before).

- [ ] **Step 3: Run the new tests**

  ```
  dotnet test Tests/Obfuscator.Tests/ --filter "InterpolatedString" -v normal
  ```

  Expected: all 3 tests **PASS**.

- [ ] **Step 4: Run the full test suite to confirm no regressions**

  ```
  dotnet test Tests/Obfuscator.Tests/ -q
  ```

  Expected: all tests pass. Note the total count — it should be the previous count + 3.

- [ ] **Step 5: Commit the implementation**

  ```
  git add Payload_Type/athena/athena/agent_code/Obfuscator/Source/Transforms/StringEncryptionTransform.cs
  git commit -m "feat: encrypt literal text portions of interpolated strings"
  ```

---

## Task 3: Validate via integration test

**Files:** No source changes — run existing integration tests.

Context: `BuildIntegrationTests` in `Tests/Obfuscator.Tests/` contains `ObfuscatedSource_CoreProjects_Build` which runs the full obfuscation pipeline through `dotnet publish`. This confirms the change produces a compilable output.

- [ ] **Step 1: Run the integration test with a 30-minute timeout**

  ```
  dotnet test Tests/Obfuscator.Tests/ --filter "ObfuscatedSource_CoreProjects_Build" -v normal --timeout 1800000
  ```

  Expected: `Passed ObfuscatedSource_CoreProjects_Build`. The obfuscated source directory compiles successfully with interpolated-string text now replaced by decryptor calls.

  If the test was previously passing, it must still pass. If it wasn't run yet, a pass here confirms end-to-end correctness.

- [ ] **Step 2: If the integration test fails, diagnose**

  The most likely failure mode is a Roslyn compilation error in the rewritten source. Check the test output for:
  - `CS0103` — `_Ns` not in scope (the decryptor namespace injection may not have run on that file)
  - `CS1525` — unexpected symbol inside interpolated string (malformed `$"..."` syntax node)

  If you see either, read the full test output to find which source file triggered the error, then inspect the rewritten AST by adding a temporary `Console.WriteLine(result.ToFullString())` in the integration test helper.

- [ ] **Step 3: Commit final state and tag**

  ```
  git add -A
  git commit -m "test: confirm interpolated string encryption passes integration build"
  ```
