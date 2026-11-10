// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models
{
    /// <summary>
    /// A timeline built in the UI's timeline editor and kept on the server, so it can be shared and run on any machine
    /// </summary>
    [Table("saved_timelines")]
    public class SavedTimeline
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        /// <summary>
        /// The handlers as the editor sent them, kept as JSON text so they come back unchanged
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string TimeLineHandlers { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }
    }
}
