import { pbkdf2Sync, randomBytes } from "node:crypto";

export function hashPassword(password: string) {
  const salt = randomBytes(16);
  const hash = pbkdf2Sync(password, salt, 120_000, 32, "sha256");
  return `pbkdf2-sha256$120000$${salt.toString("base64")}$${hash.toString("base64")}`;
}
