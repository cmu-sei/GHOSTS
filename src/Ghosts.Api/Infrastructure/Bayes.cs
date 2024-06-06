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
        this.Position = position;
        this.LikelihoodH1 = likelihood_h_1;
        this.LikelihoodH2 = likelihood_h_2;
        this.PriorH1 = prior_h_1;
        this.PriorH2 = prior_h_2;
        this.PosteriorH1 = 0;
        this.PosteriorH2 = 0;
        this.CalculatePosterior();
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
        if (((this.LikelihoodH1 * this.PriorH1) + (this.LikelihoodH2 * this.PriorH2)) > 0)
        {
            this.PosteriorH1 = (this.LikelihoodH1 * this.PriorH1) /
                               ((this.LikelihoodH1 * this.PriorH1) + (this.LikelihoodH2 * this.PriorH2));
        }
        else
        {
            this.PosteriorH1 = 0;
        }

        if (((this.LikelihoodH2 * this.PriorH2) + (this.LikelihoodH1 * this.PriorH1)) > 0)
        {
            this.PosteriorH2 = (this.LikelihoodH2 * this.PriorH2) /
                               ((this.LikelihoodH2 * this.PriorH2) + (this.LikelihoodH1 * this.PriorH1));
        }
        else
        {
            this.PosteriorH2 = 0;
        }

        this.PosteriorH1 = Normalize(this.PosteriorH1);
        this.PosteriorH2 = Normalize(this.PosteriorH2);
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