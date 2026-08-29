# Global Working Context

---

## Communication Language

- **Chat/Discussion**: Vietnamese (Tiếng Việt)
- **Code/Comments**: English
- **Technical Documentation**: English

## User Identity

- **Nickname**: Master Chu
- **Instruction**: Always address the user as "Master Chu" when replying (e.g. when opening a response or referring to them directly).

---

## Working Rules

### When Multiple Solutions Exist
- **Stop and present options** to user
- **Wait for user to choose** direction
- **Then proceed** with chosen solution
- Do NOT assume or pick solution yourself

### File Creation
- **Code files**: Create normally when needed (controllers, services, components, etc.)
- **Do NOT create documentation files** after completing tasks:
  - Note files (.md, .txt)
  - Explanation/guide files
  - Summary files
  - Documentation files (docs/, notes/)
  - README files (unless requested)
- **NEVER create .md or docs files** unless user explicitly requests
- Only create documentation when user explicitly asks for it

### Code Changes
- **Minimal changes**: Only modify what's necessary
- **Follow existing patterns**: Stay consistent with codebase
- **Verify before commit**: Test to ensure code works
- **No unrequested refactoring**: Do not modify, format, or refactor existing code outside the strict scope of the current task

---

## Testing Rule

Whenever code changes, rerun the tests relevant to the changed code before considering the change complete. Do not treat a code change as verified without recording the relevant test result.

## Change Verification and Git Push Rule

For every code or configuration change, follow this order:

1. Make the change.
2. Run the relevant tests and the required full test suite.
3. Continue only when the test run succeeds completely: zero failed tests and no unresolved build errors.
4. Record the verification result when required by the project context.
5. Commit the verified change and push it to Git.

## Git Workflow

### Commit Message Format (Conventional Commits)
```
<type>: <description>

[optional body]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `refactor`: Code refactoring (no functionality change)
- `style`: Code style changes (formatting, semicolons, etc.)
- `test`: Adding or updating tests
- `chore`: Maintenance tasks (dependencies, config, etc.)

**Examples:**
```bash
feat: add user authentication
fix: resolve login timeout issue
docs: update API documentation
refactor: restructure user service
chore: update dependencies
```

### Push Code (Preferred - Single Line)
```bash
git add . ; git commit -m "type: description" ; git push origin main
```

### If Error Occurs, Run Separately
```bash
git add .
git commit -m "type: description"
git push origin main
```

### Security
- **Do NOT push files containing sensitive information**:
  - API keys, tokens, passwords
  - Database connection strings
  - Private keys, certificates
  - `.env` files (must be in `.gitignore`)
- Always check before pushing

---

## Code Quality

### General Principles
- **Readability**: Code should be self-documenting
- **Consistency**: Follow existing patterns in codebase
- **Simplicity**: Prefer simple over complex solutions
- **Security**: Always consider security implications

### Error Handling
- **Meaningful messages**: Help with debugging
- **Proper status codes**: Use appropriate codes
- **Log with context**: Always log errors with details

### Docker Logs
- **Always use `--tail 100`**: Never use real-time logs (`-f` flag)
- **Correct command**: `docker logs <container-name> --tail 100`
- **Never use**: `docker logs -f <container-name>` (real-time monitoring)

---

## Usage Instructions

**For AI Agent:**
1. Read this GLOBAL_WORKING_CONTEXT first
2. Ask user for PROJECT_CONTEXT file
3. Read project-specific context
4. Start working with both contexts loaded

**For User:**
1. Paste this file at start of new session
2. When agent asks, paste PROJECT_CONTEXT file
3. Begin working

---

**Purpose**: Universal working standards for ALL projects  
**Scope**: Language, workflow, code quality, working style  
**Next Step**: Load project-specific context file
