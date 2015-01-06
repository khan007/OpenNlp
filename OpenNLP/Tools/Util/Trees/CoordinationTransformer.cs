﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNLP.Tools.Util.Ling;
using OpenNLP.Tools.Util.Trees.TRegex;
using OpenNLP.Tools.Util.Trees.TRegex.Tsurgeon;

namespace OpenNLP.Tools.Util.Trees
{
    /**
 * Coordination transformer transforms a PennTreebank tree containing
 * a coordination in a flat structure in order to get the dependencies
 * right.
 * <br>
 * The transformer goes through several steps:
 * <ul>
 * <li> Removes empty nodes and simplifies many tags (<code>DependencyTreeTransformer</code>)
 * <li> Relabels UCP phrases to either ADVP or NP depending on their content
 * <li> Turn flat CC structures into structures with an intervening node
 * <li> Add extra structure to QP phrases - combine "well over", unflattened structures with CC (<code>QPTreeTransformer</code>)
 * <li> Flatten SQ structures to get the verb as the head
 * <li> Rearrange structures that appear to be dates
 * <li> Flatten X over only X structures
 * <li> Turn some fixed conjunction phrases into CONJP, such as "and yet", etc
 * <li> Attach RB such as "not" to the next phrase to get the RB headed by the phrase it modifies
 * <li> Turn SBAR to PP if parsed as SBAR in phrases such as "The day after the airline was planning ..."
 * <li> Rearrange "now that" into an SBAR phrase if it was misparsed as ADVP
 * </ul>
 *
 * @author Marie-Catherine de Marneffe
 * @author John Bauer
 */

    public class CoordinationTransformer : TreeTransformer
    {
        //private static readonly bool VERBOSE = System.getProperty("CoordinationTransformer", null) != null;
        private readonly TreeTransformer tn = new DependencyTreeTransformer(); //to get rid of unwanted nodes and tag
        private readonly TreeTransformer qp = new QPTreeTransformer(); //to restructure the QP constituents
        private readonly TreeTransformer dates = new DateTreeTransformer(); //to flatten date patterns

        private readonly HeadFinder headFinder;

        // default constructor
        public CoordinationTransformer(HeadFinder hf)
        {
            this.headFinder = hf;
        }

        /**
   * Transforms t if it contains a coordination in a flat structure (CCtransform)
   * and transforms UCP (UCPtransform).
   *
   * @param t a tree to be transformed
   * @return t transformed
   */
        //@Override
        public Tree TransformTree(Tree t)
        {
            /*if (VERBOSE) {
      System.err.println("Input to CoordinationTransformer: " + t);
    }*/
            t = tn.TransformTree(t);
            /*if (VERBOSE) {
      System.err.println("After DependencyTreeTransformer:  " + t);
    }*/
            if (t == null)
            {
                return t;
            }
            t = UcpTransform(t);
            /*if (VERBOSE) {
      System.err.println("After UCPTransformer:             " + t);
    }*/
            t = CcTransform(t);
            /*if (VERBOSE) {
      System.err.println("After CCTransformer:              " + t);
    }*/
            t = qp.TransformTree(t);
            /*if (VERBOSE) {
      System.err.println("After QPTreeTransformer:          " + t);
    }*/
            t = SqFlatten(t);
            /*if (VERBOSE) {
      System.err.println("After SQ flattening:              " + t);
    }*/
            t = dates.TransformTree(t);
            /*if (VERBOSE) {
      System.err.println("After DateTreeTransformer:        " + t);
    }*/
            t = RemoveXOverX(t);
            /*if (VERBOSE) {
      System.err.println("After removeXoverX:               " + t);
    }*/
            t = CombineConjp(t);
            /*if (VERBOSE) {
      System.err.println("After combineConjp:               " + t);
    }*/
            t = MoveRb(t);
            /*if (VERBOSE) {
      System.err.println("After moveRB:                     " + t);
    }*/
            t = ChangeSbarToPp(t);
            /*if (VERBOSE) {
      System.err.println("After changeSbarToPP:             " + t);
    }*/
            t = RearrangeNowThat(t);
            /*if (VERBOSE) {
      System.err.println("After rearrangeNowThat:           " + t);
    }*/
            return t;
        }

        private static readonly TregexPattern RearrangeNowThatTregex =
            TregexPattern.Compile("ADVP=advp <1 (RB < /^(?i:now)$/) <2 (SBAR=sbar <1 (IN < /^(?i:that)$/))");

        private static readonly TsurgeonPattern RearrangeNowThatTsurgeon =
            Tsurgeon.parseOperation("[relabel advp SBAR] [excise sbar sbar]");

        private static Tree RearrangeNowThat(Tree t)
        {
            if (t == null)
            {
                return t;
            }
            return Tsurgeon.processPattern(RearrangeNowThatTregex, RearrangeNowThatTsurgeon, t);
        }


        private static readonly TregexPattern ChangeSbarToPpTregex =
            TregexPattern.Compile("NP < (NP $++ (SBAR=sbar < (IN < /^(?i:after|before|until|since|during)$/ $++ S)))");

        private static readonly TsurgeonPattern ChangeSbarToPpTsurgeon =
            Tsurgeon.parseOperation("relabel sbar PP");

        /**
   * For certain phrases, we change the SBAR to a PP to get prep/pcomp
   * dependencies.  For example, in "The day after the airline was
   * planning...", we want prep(day, after) and pcomp(after,
   * planning).  If "after the airline was planning" was parsed as an
   * SBAR, either by the parser or in the treebank, we fix that here.
   */

        private static Tree ChangeSbarToPp(Tree t)
        {
            if (t == null)
            {
                return null;
            }
            return Tsurgeon.processPattern(ChangeSbarToPpTregex, ChangeSbarToPpTsurgeon, t);
        }

        private static readonly TregexPattern FindFlatConjpTregex =
            // TODO: add more patterns, perhaps ignore case
            // for example, what should we do with "and not"?  Is it right to
            // generally add the "not" to the following tree with moveRB, or
            // should we make "and not" a CONJP?
            // also, perhaps look at ADVP
            TregexPattern.Compile("/^(S|PP|VP)/ < (/^(S|PP|VP)/ $++ (CC=start $+ (RB|ADVP $+ /^(S|PP|VP)/) " +
                                  "[ (< and $+ (RB=end < yet)) | " + // TODO: what should be the head of "and yet"?
                                  "  (< and $+ (RB=end < so)) | " +
                                  "  (< and $+ (ADVP=end < (RB|IN < so))) ] ))");
            // TODO: this structure needs a dependency

        private static readonly TsurgeonPattern AddConjpTsurgeon =
            Tsurgeon.parseOperation("createSubtree CONJP start end");

        private static Tree CombineConjp(Tree t)
        {
            if (t == null)
            {
                return null;
            }
            return Tsurgeon.processPattern(FindFlatConjpTregex, AddConjpTsurgeon, t);
        }

        private static readonly TregexPattern[] MoveRbTregex =
        {
            TregexPattern.Compile(
                "/^S|PP|VP|NP/ < (/^(S|PP|VP|NP)/ $++ (/^(,|CC|CONJP)$/ [ $+ (RB=adv [ < not | < then ]) | $+ (ADVP=adv <: RB) ])) : (=adv $+ /^(S|PP|VP|NP)/=dest) "),
            TregexPattern.Compile(
                "/^ADVP/ < (/^ADVP/ $++ (/^(,|CC|CONJP)$/ [$+ (RB=adv [ < not | < then ]) | $+ (ADVP=adv <: RB)])) : (=adv $+ /^NP-ADV|ADVP|PP/=dest)"),
            TregexPattern.Compile("/^FRAG/ < (ADVP|RB=adv $+ VP=dest)"),
        };

        private static readonly TsurgeonPattern MoveRbTsurgeon =
            Tsurgeon.parseOperation("move adv >0 dest");

        private static Tree MoveRb(Tree t)
        {
            if (t == null)
            {
                return null;
            }
            foreach (TregexPattern pattern in MoveRbTregex)
            {
                t = Tsurgeon.processPattern(pattern, MoveRbTsurgeon, t);
            }
            return t;
        }

        // Matches to be questions if the question starts with WHNP, such as
        // Who, What, if there is an SQ after the WH question.
        //
        // TODO: maybe we want to catch more complicated tree structures
        // with something in between the WH and the actual question.
        private static readonly TregexPattern FlattenSqTregex =
            TregexPattern.Compile("SBARQ < ((WHNP=what < WP) $+ (SQ=sq < (/^VB/=verb < " +
                                  EnglishPatterns.copularWordRegex + ") " +
                                  // match against "is running" if the verb is under just a VBG
                                  " !< (/^VB/ < !" + EnglishPatterns.copularWordRegex + ") " +
                                  // match against "is running" if the verb is under a VP - VBG
                                  " !< (/^V/ < /^VB/ < !" + EnglishPatterns.copularWordRegex + ") " +
                                  // match against "What is on the test?"
                                  " !< (PP $- =verb) " +
                                  // match against "is there"
                                  " !<, (/^VB/ < " + EnglishPatterns.copularWordRegex + " $+ (NP < (EX < there)))))");

        private static readonly TsurgeonPattern FlattenSqTsurgeon = Tsurgeon.parseOperation("excise sq sq");

        /**
   * Removes the SQ structure under a WHNP question, such as "Who am I
   * to judge?".  We do this so that it is easier to pick out the head
   * and then easier to connect that head to all of the other words in
   * the question in this situation.  In the specific case of making
   * the copula head, we don't do this so that the existing headfinder
   * code can easily find the "am" or other copula verb.
   */

        public Tree SqFlatten(Tree t)
        {
            if (headFinder != null && (headFinder is CopulaHeadFinder))
            {
                if (((CopulaHeadFinder) headFinder).MakesCopulaHead())
                {
                    return t;
                }
            }
            if (t == null)
            {
                return null;
            }
            return Tsurgeon.processPattern(FlattenSqTregex, FlattenSqTsurgeon, t);
        }

        private static readonly TregexPattern RemoveXOverXTregex =
            TregexPattern.Compile("__=repeat <: (~repeat < __)");

        private static readonly TsurgeonPattern RemoveXOverXTsurgeon = Tsurgeon.parseOperation("excise repeat repeat");

        public static Tree RemoveXOverX(Tree t)
        {
            return Tsurgeon.processPattern(RemoveXOverXTregex, RemoveXOverXTsurgeon, t);
        }

        // UCP (JJ ...) -> ADJP
        // UCP (DT JJ ...) -> ADJP
        // UCP (... (ADJP (JJR older|younger))) -> ADJP
        // UCP (N ...) -> NP
        // UCP ADVP -> ADVP
        // Might want to look for ways to include RB for flatter structures,
        // but then we have to watch out for (RB not) for example
        // Note that the order of OR expressions means the older|younger
        // pattern takes precedence
        // By searching for everything at once, then using one tsurgeon
        // which fixes everything at once, we can save quite a bit of time
        private static readonly TregexPattern UcpRenameTregex =
            TregexPattern.Compile("/^UCP/=ucp [ <, /^JJ|ADJP/=adjp | ( <1 DT <2 /^JJ|ADJP/=adjp ) |" +
                                  " <- (ADJP=adjp < (JJR < /^(?i:younger|older)$/)) |" +
                                  " <, /^N/=np | ( <1 DT <2 /^N/=np ) | " +
                                  " <, /^ADVP/=advp ]");

        // TODO: this turns UCP-TMP into ADVP instead of ADVP-TMP.  What do we actually want?
        private static readonly TsurgeonPattern UcpRenameTsurgeon =
            Tsurgeon.parseOperation(
                "[if exists adjp relabel ucp /^UCP(.*)$/ADJP$1/] [if exists np relabel ucp /^UCP(.*)$/NP$1/] [if exists advp relabel ucp /^UCP(.*)$/ADVP/]");

        /**
   * Transforms t if it contains an UCP, it will change the UCP tag
   * into the phrasal tag of the first word of the UCP
   * (UCP (JJ electronic) (, ,) (NN computer) (CC and) (NN building))
   * will become
   * (ADJP (JJ electronic) (, ,) (NN computer) (CC and) (NN building))
   *
   * @param t a tree to be transformed
   * @return t transformed
   */

        public static Tree UcpTransform(Tree t)
        {
            if (t == null)
            {
                return null;
            }
            return Tsurgeon.processPattern(UcpRenameTregex, UcpRenameTsurgeon, t);
        }


        /**
   * Transforms t if it contains a coordination in a flat structure
   *
   * @param t a tree to be transformed
   * @return t transformed (give t not null, return will not be null)
   */

        public static Tree CcTransform(Tree t)
        {
            bool notDone = true;
            while (notDone)
            {
                Tree cc = FindCcParent(t, t);
                if (cc != null)
                {
                    t = cc;
                }
                else
                {
                    notDone = false;
                }
            }
            return t;
        }

        private static string GetHeadTag(Tree t)
        {
            if (t.Value().StartsWith("NN"))
            {
                return "NP";
            }
            else if (t.Value().StartsWith("JJ"))
            {
                return "ADJP";
            }
            else
            {
                return "NP";
            }
        }


        /** If things match, this method destructively changes the children list
   *  of the tree t.  When this method is called, t is an NP and there must
   *  be at least two children to the right of ccIndex.
   *
   *  @param t The tree to transform a conjunction in
   *  @param ccIndex The index of the CC child
   *  @return t
   */

        private static Tree TransformCc(Tree t, int ccIndex)
        {
            /*if (VERBOSE) {
      System.err.println("transformCC in:  " + t);
    }*/
            //System.out.println(ccIndex);
            // use the factories of t to create new nodes
            TreeFactory tf = t.TreeFactory();
            LabelFactory lf = t.Label().LabelFactory();

            Tree[] ccSiblings = t.Children();

            //check if other CC
            var ccPositions = new List<int>();
            for (int i = ccIndex + 1; i < ccSiblings.Length; i++)
            {
                if (ccSiblings[i].Value().StartsWith("CC") && i < ccSiblings.Length - 1)
                {
                    // second conjunct to ensure that a CC we add isn't the last child
                    ccPositions.Add(i);
                }
            }

            // a CC b c ... -> (a CC b) c ...  with b not a DT
            string beforeSibling = ccSiblings[ccIndex - 1].Value();
            if (ccIndex == 1 &&
                (beforeSibling.Equals("DT") || beforeSibling.Equals("JJ") || beforeSibling.Equals("RB") ||
                 ! (ccSiblings[ccIndex + 1].Value().Equals("DT"))) && ! (beforeSibling.StartsWith("NP")
                                                                         || beforeSibling.Equals("ADJP")
                                                                         || beforeSibling.Equals("NNS")))
            {
                // && (ccSiblings.Length == ccIndex + 3 || !ccPositions.isEmpty())) {  // something like "soya or maize oil"
                string leftHead = GetHeadTag(ccSiblings[ccIndex - 1]);
                //create a new tree to be inserted as first child of t
                Tree left = tf.NewTreeNode(lf.NewLabel(leftHead), null);
                for (int i = 0; i < ccIndex + 2; i++)
                {
                    left.AddChild(ccSiblings[i]);
                }
                /*if (VERBOSE) {
        System.out.println("print left tree");
        left.pennPrint();
        System.out.println();
      }*/

                // remove all the children of t before ccIndex+2
                for (int i = 0; i < ccIndex + 2; i++)
                {
                    t.RemoveChild(0);
                }
                /*if (VERBOSE)
                {
                    if (t.numChildren() == 0) { System.out.println("Youch! No t children"); }
                }*/

                // if stuff after (like "soya or maize oil and vegetables")
                // we need to put the tree in another tree
                if (ccPositions.Any())
                {
                    bool comma = false;
                    int index = ccPositions[0];
                    /*if (VERBOSE) {System.err.println("more CC index " +  index);}s*/
                    if (ccSiblings[index - 1].Value().Equals(","))
                    {
//to handle the case of a comma ("soya and maize oil, and vegetables")
                        index = index - 1;
                        comma = true;
                    }
                    /*if (VERBOSE) {System.err.println("more CC index " +  index);}*/
                    string head = GetHeadTag(ccSiblings[index - 1]);

                    if (ccIndex + 2 < index)
                    {
                        Tree tree = tf.NewTreeNode(lf.NewLabel(head), null);
                        tree.AddChild(0, left);

                        int k = 1;
                        for (int j = ccIndex + 2; j < index; j++)
                        {
                            /*if (VERBOSE) ccSiblings[j].pennPrint();*/
                            t.RemoveChild(0);
                            tree.AddChild(k, ccSiblings[j]);
                            k++;
                        }

                        /*if (VERBOSE) {
            System.out.println("print t");
            t.pennPrint();

            System.out.println("print tree");
            tree.pennPrint();
            System.out.println();
          }*/
                        t.AddChild(0, tree);
                    }
                    else
                    {
                        t.AddChild(0, left);
                    }

                    Tree rightTree = tf.NewTreeNode(lf.NewLabel("NP"), null);
                    int start = 2;
                    if (comma)
                    {
                        start++;
                    }
                    while (start < t.NumChildren())
                    {
                        Tree sib = t.GetChild(start);
                        t.RemoveChild(start);
                        rightTree.AddChild(sib);
                    }
                    t.AddChild(rightTree);
                }
                else
                {
                    t.AddChild(0, left);
                }
            }
                // DT a CC b c -> DT (a CC b) c
            else if (ccIndex == 2 && ccSiblings[0].Value().StartsWith("DT") &&
                     !ccSiblings[ccIndex - 1].Value().Equals("NNS") &&
                     (ccSiblings.Length == 5 || (ccPositions.Any() && ccPositions[0] == 5)))
            {
                string head = GetHeadTag(ccSiblings[ccIndex - 1]);
                //create a new tree to be inserted as second child of t (after the determiner
                Tree child = tf.NewTreeNode(lf.NewLabel(head), null);

                for (int i = 1; i < ccIndex + 2; i++)
                {
                    child.AddChild(ccSiblings[i]);
                }
                /*if (VERBOSE) { if (child.numChildren() == 0) { System.out.println("Youch! No child children"); } }*/

                // remove all the children of t between the determiner and ccIndex+2
                //System.out.println("print left tree");
                //child.pennPrint();

                for (int i = 1; i < ccIndex + 2; i++)
                {
                    t.RemoveChild(1);
                }

                t.AddChild(1, child);
            }

                // ... a, b CC c ... -> ... (a, b CC c) ...
            else if (ccIndex > 2 && ccSiblings[ccIndex - 2].Value().Equals(",") &&
                     !ccSiblings[ccIndex - 1].Value().Equals("NNS"))
            {
                string head = GetHeadTag(ccSiblings[ccIndex - 1]);
                Tree child = tf.NewTreeNode(lf.NewLabel(head), null);

                for (int j = ccIndex - 3; j < ccIndex + 2; j++)
                {
                    child.AddChild(ccSiblings[j]);
                }
                /*if (VERBOSE) { if (child.numChildren() == 0) { System.out.println("Youch! No child children"); } }*/

                int i = ccIndex - 4;
                while (i > 0 && ccSiblings[i].Value().Equals(","))
                {
                    child.AddChild(0, ccSiblings[i]); // add the comma
                    child.AddChild(0, ccSiblings[i - 1]); // add the word before the comma
                    i = i - 2;
                }

                if (i < 0)
                {
                    i = -1;
                }

                // remove the old children
                for (int j = i + 1; j < ccIndex + 2; j++)
                {
                    t.RemoveChild(i + 1);
                }
                // put the new tree
                t.AddChild(i + 1, child);
            }

                // something like "the new phone book and tour guide" -> multiple heads
                // we want (NP the new phone book) (CC and) (NP tour guide)
            else
            {
                bool commaLeft = false;
                bool commaRight = false;
                bool preconj = false;
                int indexBegin = 0;
                Tree conjT = tf.NewTreeNode(lf.NewLabel("CC"), null);

                // create the left tree
                string leftHead = GetHeadTag(ccSiblings[ccIndex - 1]);
                Tree left = tf.NewTreeNode(lf.NewLabel(leftHead), null);


                // handle the case of a preconjunct (either, both, neither)
                Tree first = ccSiblings[0];
                string leaf = first.FirstChild().Value().ToLower();
                if (leaf.Equals("either") || leaf.Equals("neither") || leaf.Equals("both"))
                {
                    preconj = true;
                    indexBegin = 1;
                    conjT.AddChild(first.FirstChild());
                }

                for (int i = indexBegin; i < ccIndex - 1; i++)
                {
                    left.AddChild(ccSiblings[i]);
                }
                // handle the case of a comma ("GM soya and maize, and food ingredients")
                if (ccSiblings[ccIndex - 1].Value().Equals(","))
                {
                    commaLeft = true;
                }
                else
                {
                    left.AddChild(ccSiblings[ccIndex - 1]);
                }

                // create the CC tree
                Tree cc = ccSiblings[ccIndex];

                // create the right tree
                int nextCc;
                if (!ccPositions.Any())
                {
                    nextCc = ccSiblings.Length;
                }
                else
                {
                    nextCc = ccPositions[0];
                }
                string rightHead = GetHeadTag(ccSiblings[nextCc - 1]);
                Tree right = tf.NewTreeNode(lf.NewLabel(rightHead), null);
                for (int i = ccIndex + 1; i < nextCc - 1; i++)
                {
                    right.AddChild(ccSiblings[i]);
                }
                // handle the case of a comma ("GM soya and maize, and food ingredients")
                if (ccSiblings[nextCc - 1].Value().Equals(","))
                {
                    commaRight = true;
                }
                else
                {
                    right.AddChild(ccSiblings[nextCc - 1]);
                }

                /*if (VERBOSE) {
        if (left.numChildren() == 0) { System.out.println("Youch! No left children"); }
        if (right.numChildren() == 0) { System.out.println("Youch! No right children"); }
      }*/

                // put trees together in old t, first we remove the old nodes
                for (int i = 0; i < nextCc; i++)
                {
                    t.RemoveChild(0);
                }
                if (ccPositions.Any())
                {
                    // need an extra level
                    Tree tree = tf.NewTreeNode(lf.NewLabel("NP"), null);

                    if (preconj)
                    {
                        tree.AddChild(conjT);
                    }
                    if (left.NumChildren() > 0)
                    {
                        tree.AddChild(left);
                    }
                    if (commaLeft)
                    {
                        tree.AddChild(ccSiblings[ccIndex - 1]);
                    }
                    tree.AddChild(cc);
                    if (right.NumChildren() > 0)
                    {
                        tree.AddChild(right);
                    }
                    if (commaRight)
                    {
                        t.AddChild(0, ccSiblings[nextCc - 1]);
                    }
                    t.AddChild(0, tree);
                }
                else
                {
                    if (preconj)
                    {
                        t.AddChild(conjT);
                    }
                    if (left.NumChildren() > 0)
                    {
                        t.AddChild(left);
                    }
                    if (commaLeft)
                    {
                        t.AddChild(ccSiblings[ccIndex - 1]);
                    }
                    t.AddChild(cc);
                    if (right.NumChildren() > 0)
                    {
                        t.AddChild(right);
                    }
                    if (commaRight)
                    {
                        t.AddChild(ccSiblings[nextCc - 1]);
                    }
                }
            }

            /*if (VERBOSE) {
      System.err.println("transformCC out: " + t);
    }*/
            return t;
        }

        private static bool NotNp(List<Tree> children, int ccIndex)
        {
            for (int i = ccIndex, sz = children.Count; i < sz; i++)
            {
                if (children[i].Value().StartsWith("NP"))
                {
                    return false;
                }
            }
            return true;
        }

        /*
   * Given a tree t, if this tree contains a CC inside a NP followed by 2 nodes
   * (i.e. we have a flat structure that will not work for the dependencies),
   * it will call transform CC on the NP containing the CC and the index of the
   * CC, and then return the root of the whole transformed tree.
   * If it finds no such tree, this method returns null.
   */

        private static Tree FindCcParent(Tree t, Tree root)
        {
            if (t.IsPreTerminal())
            {
                if (t.Value().StartsWith("CC"))
                {
                    Tree parent = t.Parent(root);
                    if (parent != null && parent.Value().StartsWith("NP"))
                    {
                        List<Tree> children = parent.GetChildrenAsList();
                        //System.out.println(children);
                        int ccIndex = children.IndexOf(t);
                        if (children.Count > ccIndex + 2 && NotNp(children, ccIndex) && ccIndex != 0 &&
                            (ccIndex == children.Count - 1 || !children[ccIndex + 1].Value().StartsWith("CC")))
                        {
                            TransformCc(parent, ccIndex);
                            /*if (VERBOSE) {
              System.err.println("After transformCC:             " + root);
            }*/
                            return root;
                        }
                    }
                }
            }
            else
            {
                foreach (Tree child in t.GetChildrenAsList())
                {
                    Tree cur = FindCcParent(child, root);
                    if (cur != null)
                    {
                        return cur;
                    }
                }
            }
            return null;
        }

    }
}