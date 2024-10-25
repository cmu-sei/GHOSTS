// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using AutoMapper;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Animator.Models;

namespace ghosts.api.Infrastructure.Models;

[Table("npcs")]
public class NpcRecord
{
    [Key]
    public Guid Id { get; set; }

    public Guid? MachineId { get; set; }

    /// <summary>
    /// Used for grouping NPCs together, e.g. 2020, 2021
    /// </summary>
    public string Campaign { get; set; }

    /// <summary>
    /// Used for grouping NPCs together, e.g.
    /// We could call this a group but in order
    /// to be more specific we use enclave.
    /// </summary>
    public string Enclave { get; set; }

    /// <summary>
    /// Used for grouping NPCs together, e.g. 
    /// A team within an enclave
    /// </summary>
    public string Team { get; set; }

    // this is also currently jsonb
    public NpcProfile NpcProfile { get; set; }

    // jsonb

    public NpcSocialGraph NpcSocialGraph { get; set; }

    public static NpcRecord TransformToNpc(NpcProfile o)
    {
        if (o == null) return new NpcRecord();

        var n = new NpcRecord
        {
            NpcProfile = o,
            NpcSocialGraph = new NpcSocialGraph(),
            Id = o.Id
        };

        return n;
    }

    /// <summary>
    /// Summary only copies the first record for many of the lists a profile might have
    /// Often used to submit to a system such as an LLM where a full profile would be too much data
    /// </summary>
    public static NpcProfileSummary TransformToNpcProfileSummary(NpcProfile o)
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<NpcProfile, NpcProfileSummary>()
                .ForMember(dest => dest.Address,
                    opt => opt.MapFrom(src => src.Address.FirstOrDefault()))
                .ForMember(dest => dest.Education.Degrees,
                    opt => opt.MapFrom(src => src.Education.Degrees.FirstOrDefault()))
                .ForMember(dest => dest.Employment.EmploymentRecords,
                    opt => opt.MapFrom(src => src.Employment.EmploymentRecords.FirstOrDefault()))
                .ForMember(dest => dest.ForeignTravel.Trips,
                opt => opt.MapFrom(src => src.ForeignTravel.Trips.FirstOrDefault()));
        });

        var mapper = new Mapper(config);
        return mapper.Map<NpcProfileSummary>(o);
    }
}
