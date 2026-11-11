// PactoGG · Script de la base de datos (MongoDB Atlas M0).
//
// Crea las 15 colecciones, sus validadores (en modo "warn": documentan el esquema sin bloquear
// a la API) y TODOS los índices con los mismos nombres y llaves que crea la API al arrancar
// (Pactogg.Api/Infrastructure/Mongo/MongoIndexInitializer.cs). Es idempotente: se puede correr
// varias veces sin duplicar nada.
//
//   mongosh "mongodb+srv://USUARIO:CLAVE@cluster.xxxxx.mongodb.net/" database/mongo-init.js
//   PACTOGG_DB=pactogg_test mongosh "<cadena>" database/mongo-init.js     # otra base
//
// No contiene datos personales ni secretos. Los puntos NO tienen valor monetario.

const dbName = (typeof process !== "undefined" && process.env.PACTOGG_DB) || "pactogg";
const target = db.getSiblingDB(dbName);
print(`Base de datos: ${dbName}`);

const oid = { bsonType: "objectId" };
const date = { bsonType: "date" };
const str = (min, max) => ({ bsonType: "string", minLength: min, maxLength: max });

const collections = {
  users: {
    required: ["email", "nickname", "nickname_lower", "status", "created_at"],
    properties: {
      email: str(3, 254),
      nickname: { bsonType: "string", pattern: "^[A-Za-z0-9_.-]{3,24}$" },
      nickname_lower: str(3, 24),
      password_hash: { bsonType: "string" },
      google_sub: { bsonType: ["string", "null"] },
      avatar_id: str(1, 32),
      email_verified: { bsonType: "bool" },
      status: { enum: ["active", "deleted", "suspended"] },
      created_at: date
    }
  },
  refresh_tokens: { required: ["user_id", "token_hash", "expires_at"], properties: { user_id: oid, token_hash: { bsonType: "string" }, expires_at: date } },
  groups: {
    required: ["name", "owner_id", "invite_code", "member_count"],
    properties: {
      name: str(2, 40),
      owner_id: oid,
      invite_code: { bsonType: "string", pattern: "^[A-Z0-9]{6}$" },
      member_count: { bsonType: "int", minimum: 0, maximum: 20 }
    }
  },
  group_members: {
    required: ["group_id", "user_id", "role", "points_balance", "status"],
    properties: {
      group_id: oid,
      user_id: oid,
      role: { enum: ["admin", "member"] },
      points_balance: { bsonType: "int", minimum: 0 },
      status: { enum: ["active", "left", "removed"] }
    }
  },
  contracts: {
    required: ["group_id", "challenger_id", "target_type", "game", "condition", "reward", "deadline", "bond_required", "status", "version"],
    properties: {
      group_id: oid,
      challenger_id: oid,
      target_type: { enum: ["group", "member"] },
      reward: str(3, 80),
      bond_required: { bsonType: "int", minimum: 0 },
      status: { enum: ["open", "in_progress", "voting", "fulfilled", "cheating", "expired", "cancelled"] },
      version: { bsonType: "int", minimum: 1 }
    }
  },
  evidence: {
    required: ["contract_id", "user_id", "type", "created_at"],
    properties: { contract_id: oid, user_id: oid, type: { enum: ["photo", "link"] } }
  },
  votes: {
    required: ["contract_id", "juror_id", "vote", "created_at"],
    properties: { contract_id: oid, juror_id: oid, vote: { enum: ["legit", "cheat"] }, comment: { bsonType: ["string", "null"], maxLength: 140 } }
  },
  points_ledger: {
    required: ["group_id", "user_id", "type", "amount", "ref_type", "ref_id", "balance_after", "created_at"],
    properties: {
      type: { enum: ["bond_lock", "bond_release", "bond_forfeit", "reward", "tournament_entry", "tournament_prize", "adjustment"] },
      amount: { bsonType: "int" },
      balance_after: { bsonType: "int", minimum: 0 }
    }
  },
  commitments: {
    required: ["group_id", "contract_id", "debtor_id", "creditor_id", "description", "status"],
    properties: { status: { enum: ["pending", "completion_requested", "completed", "cancelled"] }, description: str(3, 80) }
  },
  notifications: { required: ["user_id", "type", "title", "created_at"], properties: { user_id: oid, title: str(1, 80) } },
  devices: { required: ["user_id", "fcm_token", "platform"], properties: { user_id: oid, fcm_token: str(20, 4096) } },
  tournaments: { required: ["group_id", "game", "status"], properties: { status: { enum: ["open", "in_progress", "finished", "cancelled"] } } },
  matches: { required: ["tournament_id", "round", "position", "status"], properties: { tournament_id: oid } },
  audit_events: { required: ["action", "entity_type", "created_at"], properties: { action: { bsonType: "string" } } },
  reports: {
    required: ["group_id", "reporter_id", "target_type", "target_id", "reason", "status", "created_at"],
    properties: { target_type: { enum: ["evidence", "comment", "user"] }, status: { enum: ["open", "dismissed", "content_removed", "escalated"] } }
  },
  blocks: { required: ["blocker_id", "blocked_id", "created_at"], properties: { blocker_id: oid, blocked_id: oid } },
  achievements: {
    required: ["user_id", "type", "code", "source_type", "source_id", "earned_at"],
    properties: { user_id: oid, type: { enum: ["badge", "streak", "trophy"] }, source_type: { enum: ["contract", "tournament"] } }
  }
};

const existing = new Set(target.getCollectionNames());
for (const [name, schema] of Object.entries(collections)) {
  const validator = { $jsonSchema: { bsonType: "object", required: schema.required, properties: schema.properties } };
  if (existing.has(name)) {
    target.runCommand({ collMod: name, validator, validationLevel: "moderate", validationAction: "warn" });
    print(`  ✓ ${name} (validador actualizado)`);
  } else {
    target.createCollection(name, { validator, validationLevel: "moderate", validationAction: "warn" });
    print(`  + ${name} (creada)`);
  }
}

// Índices: mismos nombres y llaves que MongoIndexInitializer.cs.
const indexes = {
  users: [
    [{ email: 1 }, { name: "ux_users_email", unique: true }],
    [{ nickname_lower: 1 }, { name: "ux_users_nickname_lower", unique: true }],
    [{ google_sub: 1 }, { name: "ux_users_google_sub", unique: true, partialFilterExpression: { google_sub: { $type: "string" } } }]
  ],
  refresh_tokens: [
    [{ token_hash: 1 }, { name: "ux_refresh_token_hash", unique: true }],
    [{ user_id: 1, expires_at: 1 }, { name: "ix_refresh_user_expiry" }]
  ],
  groups: [[{ invite_code: 1 }, { name: "ux_groups_invite_code", unique: true }]],
  group_members: [
    [{ group_id: 1, user_id: 1 }, { name: "ux_group_members_group_user", unique: true }],
    [{ user_id: 1, status: 1 }, { name: "ix_group_members_user_status" }],
    [{ group_id: 1, nick_lower: 1 }, { name: "ix_group_members_group_nick" }],
    [{ group_id: 1, points_balance: -1 }, { name: "ix_group_members_group_points" }]
  ],
  contracts: [
    [{ group_id: 1, status: 1, deadline: 1 }, { name: "ix_contracts_group_status_deadline" }],
    [{ group_id: 1, _id: -1 }, { name: "ix_contracts_group_created" }],
    [{ jurors: 1, status: 1 }, { name: "ix_contracts_jurors_status" }]
  ],
  evidence: [
    [{ contract_id: 1, created_at: 1 }, { name: "ix_evidence_contract_created" }],
    [{ expires_at: 1 }, { name: "ix_evidence_expires_photo", partialFilterExpression: { type: "photo" } }]
  ],
  votes: [[{ contract_id: 1, juror_id: 1 }, { name: "ux_votes_contract_juror", unique: true }]],
  points_ledger: [
    [{ ref_id: 1, type: 1, user_id: 1 }, { name: "ux_points_ledger_ref_type_user", unique: true }],
    [{ group_id: 1, user_id: 1, created_at: -1 }, { name: "ix_points_ledger_user_group_date" }]
  ],
  commitments: [
    [{ contract_id: 1 }, { name: "ux_commitments_contract", unique: true }],
    [{ group_id: 1, debtor_id: 1, status: 1 }, { name: "ix_commitments_group_debtor_status" }],
    [{ group_id: 1, creditor_id: 1, status: 1 }, { name: "ix_commitments_group_creditor_status" }],
    [{ status: 1, created_at: 1 }, { name: "ix_commitments_status_created" }]
  ],
  devices: [
    [{ fcm_token: 1 }, { name: "ux_devices_fcm_token", unique: true }],
    [{ user_id: 1 }, { name: "ix_devices_user" }]
  ],
  notifications: [
    [{ user_id: 1, read_at: 1, _id: -1 }, { name: "ix_notifications_user_read" }],
    [{ created_at: 1 }, { name: "ttl_notifications_30d", expireAfterSeconds: 60 * 60 * 24 * 30 }],
    [{ dedupe_key: 1 }, { name: "ux_notifications_dedupe", unique: true, partialFilterExpression: { dedupe_key: { $type: "string" } } }]
  ],
  tournaments: [[{ group_id: 1, status: 1, scheduled_at: 1 }, { name: "ix_tournaments_group_status_scheduled" }]],
  audit_events: [[{ group_id: 1, created_at: -1 }, { name: "ix_audit_group_created" }], [{ group_id: 1, _id: -1 }, { name: "ix_audit_group_id" }]],
  reports: [
    [{ reporter_id: 1, target_type: 1, target_id: 1 }, { name: "ux_reports_reporter_target", unique: true }],
    [{ group_id: 1, status: 1, _id: -1 }, { name: "ix_reports_group_status" }]
  ],
  blocks: [
    [{ blocker_id: 1, blocked_id: 1 }, { name: "ux_blocks_pair", unique: true }],
    [{ blocked_id: 1 }, { name: "ix_blocks_blocked" }]
  ],
  achievements: [
    [{ user_id: 1, code: 1, source_key: 1 }, { name: "ux_achievements_user_code_source", unique: true }],
    [{ user_id: 1, earned_at: -1 }, { name: "ix_achievements_user_earned" }]
  ]
};

let total = 0;
for (const [collection, list] of Object.entries(indexes)) {
  for (const [keys, options] of list) {
    target.getCollection(collection).createIndex(keys, options);
    total++;
  }
}
print(`Índices verificados: ${total}`);
print("Listo.");
