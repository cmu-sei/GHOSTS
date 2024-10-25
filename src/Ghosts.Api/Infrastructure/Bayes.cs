// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;

namespace ghosts.api.Areas.Animator.Infrastructure;

public class Bayes
{
    public readonly decimal LikelihoodH1;
    public readonly decimal PriorH1;
    public readonly decimal LikelihoodH2;
    public readonly decimal PriorH2;
    public decimal PosteriorH1;
    public decimal PosteriorH2;
    public long Position;

    /// <summary>
    /// straight iterative bayes calculation, where priors become the previous posterior
    /// </summary>
    public Bayes(long position, decimal likelihood_h_1, decimal prior_h_1, decimal likelihood_h_2, decimal prior_h_2)
    {
        Position = position;
        LikelihoodH1 = likelihood_h_1;
        LikelihoodH2 = likelihood_h_2;
        PriorH1 = prior_h_1;
        PriorH2 = prior_h_2;
        PosteriorH1 = 0;
        PosteriorH2 = 0;
        CalculatePosterior();
    }

    /// <summary>
    /// Bayes calculation e.g.
    /// Likelihood(H_1)
    /// Prior(H_1)
    /// Likelihood(MG)
    /// Prior(H1)
    /// Likelihood(MB)
    /// Prior(H2)
    /// </summary>
    private void CalculatePosterior()
    {
        if (((LikelihoodH1 * PriorH1) + (LikelihoodH2 * PriorH2)) > 0)
        {
            PosteriorH1 = (LikelihoodH1 * PriorH1) /
                               ((LikelihoodH1 * PriorH1) + (LikelihoodH2 * PriorH2));
        }
        else
        {
            PosteriorH1 = 0;
        }

        if (((LikelihoodH2 * PriorH2) + (LikelihoodH1 * PriorH1)) > 0)
        {
            PosteriorH2 = (LikelihoodH2 * PriorH2) /
                               ((LikelihoodH2 * PriorH2) + (LikelihoodH1 * PriorH1));
        }
        else
        {
            PosteriorH2 = 0;
        }

        PosteriorH1 = Normalize(PosteriorH1);
        PosteriorH2 = Normalize(PosteriorH2);
    }

    private static decimal Normalize(decimal n)
    {
        if (n > 1)
        {
            n = 1;
        }

        if (n < 0)
        {
            n = 0;
        }

        return Math.Round(n, 10);
    }
}
