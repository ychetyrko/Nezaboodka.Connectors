/*********************************************
		Alter database schema
*/
/*	Protocol for altering database schema:

	1. call `before_alter_database_schema`;
	2. fill `type_add_list`, `type_rem_list`, `field_add_list` and `field_rem_list` tables;
	3. call `alter_database_schema`.
*/

#include "schema/public.sql"

#include "schema/common.sql"

#include "schema/field.sql"

#include "schema/type.sql"

#include "schema/backref.sql"
