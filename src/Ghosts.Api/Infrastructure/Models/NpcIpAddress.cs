// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

[Table("ips")]
public class NpcIpAddress
{
    [Key]
    public int Id { get; set; }
    public Guid NpcId { get; set; }
    public string IpAddress { get; set; }
    public DateTime CreatedUTC { get; set; }

    public string Enclave { get; set; }

    public NpcIpAddress()
    {
        CreatedUTC = DateTime.UtcNow;
    }
}
