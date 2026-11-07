using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Text;

namespace Reviews.Domain.Common
{
    public abstract class BaseEntity
    {
        private bool _versionBumped;

        public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public string CreatedBy { get; private set; } = string.Empty;   
        public string UpdatedBy { get; private set; } = string.Empty;  
        public bool IsDeleted { get; private set; }
        public int Version { get; private set; }
        public int ExpectedVersion => _versionBumped ? Version - 1 : Version;
        protected BaseEntity() { }

        protected BaseEntity(string createdBy)
        {
            var now = DateTime.UtcNow;
            CreatedAt = UpdatedAt = now;
            CreatedBy = UpdatedBy = Guard.NotBlank(createdBy, nameof(createdBy), 128);
            Version = 1;
            _versionBumped = true;          
        }
        protected void Touch(string updatedBy)
        {
            UpdatedAt = DateTime.UtcNow;
            UpdatedBy = Guard.NotBlank(updatedBy, nameof(updatedBy), 128);
            if (!_versionBumped)
            {
                Version++;
                _versionBumped = true;
            }
        }

        protected void MarkDeleted(string deletedBy)
        {
            IsDeleted = true;
            Touch(deletedBy);
        }
    }
}
