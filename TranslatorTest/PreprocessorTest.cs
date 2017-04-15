using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CM3D2.AutoTranslate.Plugin;

namespace TranslatorTest
{
	[TestClass]
	public class PreprocessorTest
	{
		private TextPreprocessor pp = new TextPreprocessor();
		private string testString = "The quick yellow fox jumps over something...";

		[TestMethod]
		public void NoReplacements()
		{
			var processed = pp.Preprocess(testString);
			Assert.AreEqual(testString, processed, "Empty replacement list should not change string!");
		}

		[TestMethod]
		public void CheckValid()
		{
			Assert.IsFalse(pp.Valid);
			pp.AddReplacements(new Dictionary<char, string>());
			Assert.IsFalse(pp.Valid);
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{' ', "$"}
			});
			Assert.IsTrue(pp.Valid);
		}

		[TestMethod]
		public void AsciiReplaceSingle()
		{
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{' ', "$"}
			});

			var expected = testString.Replace(' ', '$');
			var processed = pp.Preprocess(testString);
			Assert.AreEqual(expected, processed, "Replacement with single ASCII char failed");
		}

		[TestMethod]
		public void AsciiReplaceMultiple()
		{
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{'e', "$"},
				{'i', "#"},
				{'l', "%"}
			});
			var expected = testString.Replace('e', '$').Replace('i','#').Replace('l','%');
			var processed = pp.Preprocess(testString);
			Assert.AreEqual(expected, processed, "Replacement with multiple ASCII char failed");
		}

		[TestMethod]
		public void AsciiReplaceWithStringSingle()
		{
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{' ', "SPACE"}
			});
			var expected = "TheSPACEquickSPACEyellowSPACEfoxSPACEjumpsSPACEoverSPACEsomething...";
			var processed = pp.Preprocess(testString);
			Assert.AreEqual(expected, processed);
		}

		[TestMethod]
		public void AsciiReplaceWithStringMultiple()
		{
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{' ', "SPACE"},
				{'.', "dot" }
			});
			var expected = "TheSPACEquickSPACEyellowSPACEfoxSPACEjumpsSPACEoverSPACEsomethingdotdotdot";
			var processed = pp.Preprocess(testString);
			Assert.AreEqual(expected, processed);
		}

		[TestMethod]
		public void TestUnicode()
		{
			//…, 『,』, ｢,｣,「,」
			pp.AddReplacements(new Dictionary<char, string>()
			{
				{ '…', "..."},
				{'『', "["},
				{'』', "]"},
				{'｢',  "["},
				{'｣',  "]"},
				{'「', "["},
				{'」', "]"} 
			});
			var unicode_test = "『Unicode』 ｢has｣ 「way」 too much …";
			var expected = "[Unicode] [has] [way] too much ...";
			var processed = pp.Preprocess(unicode_test);
			Assert.AreEqual(expected, processed);
		}
	}
}
