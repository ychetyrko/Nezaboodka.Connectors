/*********************************************

	Create Nezaboodka administrative database
		and grant all rights to nezaboodka user for it

**********************************************/

CREATE DATABASE `nz_admin_db`;
USE `nz_admin_db`;

/*********************************************

		Administrative database tables

**********************************************/

/*********************************************
	Databases list to store user rights:
		0 - ReadWrite
		1 - ReadOnly
		2 - NoAccess
*/
CREATE TABLE `db_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE,
	`access` TINYINT NOT NULL DEFAULT 0	# ReadWrite
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

CREATE TABLE `db_trash_list`(
	`name` VARCHAR(64) PRIMARY KEY NOT NULL UNIQUE,
	`access` TINYINT NOT NULL
) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;

/*********************************************

		Stored procedures and functions

**********************************************/

DELIMITER //

/*********************************************
	Prepare empty database for work
*/
CREATE PROCEDURE prepare_db(db_name VARCHAR(64))
BEGIN
# ============= Schema Info ==================
# > Type
	SET @prep_str=CONCAT('
		CREATE TABLE `', db_name, '`.`type` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`name` VARCHAR(128) NOT NULL UNIQUE,
			`table_name` VARCHAR(64) NOT NULL UNIQUE  COLLATE `utf8_general_ci`,
			`base_type_name` VARCHAR(128) NOT NULL,
			`base_type_id` INT DEFAULT NULL,
			
			FOREIGN KEY(`base_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE
		) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
	');
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
# > Field
	SET @prep_str=CONCAT("
		CREATE TABLE `", db_name, "`.`field` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_type_name` VARCHAR(128) NOT NULL,
			`owner_type_id` INT DEFAULT NULL,
			`name` VARCHAR(128) NOT NULL,
			`col_name` VARCHAR(64) NOT NULL COLLATE `utf8_general_ci`,
			`type_name` VARCHAR(64) NOT NULL,
			`ref_type_id` INT DEFAULT NULL,
			`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
			`compare_options` ENUM
				(
					'None',
					'IgnoreCase',
					'IgnoreNonSpace',
					'IgnoreSymbols',
					'IgnoreKanaType',
					'IgnoreWidth',
					'OrdinalIgnoreCase',
					'StringSort',
					'Ordinal'
				) NOT NULL DEFAULT 'None',
			`back_ref_name` VARCHAR(128) DEFAULT NULL,
			`back_ref_id` INT DEFAULT NULL,
			
			FOREIGN KEY(`owner_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
				
			FOREIGN KEY(`ref_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE RESTRICT,
			
			FOREIGN KEY(`back_ref_id`)
				REFERENCES `field`(`id`)
				ON DELETE SET NULL
		) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
	");
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
# > Type-Field mapping
	SET @prep_str=CONCAT('
		CREATE TABLE `', db_name, '`.`type_field_map` (
			`type_id` INT NOT NULL,
			`field_id` INT NOT NULL,
			
			FOREIGN KEY (`type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
				
			FOREIGN KEY (`field_id`)
				REFERENCES `field`(`id`)
				ON DELETE CASCADE
		) ENGINE=`InnoDB`;
	');
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
# ============= Structure ==================
# > DbKey
	SET @prep_str=CONCAT('
		CREATE TABLE `', db_name, '`.`db_key` (
			`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
			`real_type_id` INT NOT NULL,
			
			FOREIGN KEY (`real_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE
		) ENGINE=`InnoDB`;
	');
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
# > Lists
	SET @prep_str=CONCAT('
		CREATE TABLE `', db_name, '`.`list` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_id` BIGINT(0) NOT NULL,
			`type_id` INT NOT NULL,
			
			FOREIGN KEY (`owner_id`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE,
			FOREIGN KEY (`type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE
		) ENGINE=`InnoDB`;
	');
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
# > ListItems
	SET @prep_str=CONCAT('
		CREATE TABLE `', db_name, '`.`list_item` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`list_id` INT NOT NULL,
			`ref` BIGINT(0) NOT NULL,
			
			FOREIGN KEY (`list_id`)
				REFERENCES `list`(`id`)
				ON DELETE CASCADE,
			FOREIGN KEY (`ref`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE
		) ENGINE=`InnoDB`;
	');
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
	
END //


/*********************************************
	Effectively alter database list
*/

CREATE PROCEDURE before_alter_database_list ()
BEGIN
# List of databases to create
	SET @prep_str=
		'CREATE TEMPORARY TABLE IF NOT EXISTS `db_add_list`(
			`name` VARCHAR(64) NOT NULL UNIQUE
		) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;';
		
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;

	SET @prep_str=
		'TRUNCATE `db_add_list`;';

	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;

# List of databases to drop
	SET @prep_str=
		'CREATE TEMPORARY TABLE IF NOT EXISTS `db_rem_list`(
			`name` VARCHAR(64) NOT NULL UNIQUE
		) ENGINE=`MEMORY` DEFAULT CHARSET=`utf8` COLLATE `utf8_general_ci`;';
		
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;

	SET @prep_str=
		'TRUNCATE `db_rem_list`;';
	
	PREPARE proc_prep FROM @prep_str;
	EXECUTE proc_prep;
	DEALLOCATE PREPARE proc_prep;
END //

CREATE PROCEDURE remove_databases ()
BEGIN
	INSERT IGNORE INTO `db_trash_list` (`name`, `access`)
		SELECT ls.`name`, ls.`access` FROM `db_list` AS ls
		INNER JOIN `db_rem_list` AS rm
		ON ls.`name` = rm.`name`;

	DELETE IGNORE FROM ls
		USING `db_list` AS ls
		INNER JOIN `db_rem_list` AS rm
		WHERE ls.`name` = rm.`name`;
	
	TRUNCATE TABLE `db_rem_list`;
END //

CREATE PROCEDURE cleanup_removed_databases ()
BEGIN
	DECLARE done INT DEFAULT FALSE;
	DECLARE db_name VARCHAR(64);
	DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_trash_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
	
	OPEN cur;
	
	proc_loop: LOOP
		FETCH cur INTO db_name;
		IF done THEN
			LEAVE proc_loop;
		END IF;
		
		SET @prep_str=CONCAT('
			DROP DATABASE IF EXISTS ', db_name, ';
		');
		PREPARE proc_prep FROM @prep_str;
		EXECUTE proc_prep;
		DEALLOCATE PREPARE proc_prep;

	END LOOP;
	
	CLOSE cur;
	
	TRUNCATE TABLE `db_trash_list`;
END //

CREATE PROCEDURE add_databases ()
BEGIN
	DECLARE done INT DEFAULT FALSE;
	DECLARE db_name VARCHAR(64);
	DECLARE db_exists INT DEFAULT 0;
	DECLARE cur CURSOR FOR
		SELECT `name` FROM `db_add_list`;
	DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
	
	OPEN cur;
	
	proc_loop: LOOP
		FETCH cur INTO db_name;
		IF done THEN
			LEAVE proc_loop;
		END IF;

		# Using prepared statement argument as database name is restricted => 
		SET @prep_str=CONCAT('
			CREATE DATABASE `', db_name, '`;
		');
		PREPARE p_create_db FROM @prep_str;
		EXECUTE p_create_db;
		DEALLOCATE PREPARE p_create_db;

		# Inserting values in prepared statement is not available yet =>
		SET @prep_str=CONCAT('
			INSERT INTO `db_list` (`name`) value (\'', db_name ,'\');
		');
		PREPARE p_add_to_db FROM @prep_str;
		EXECUTE p_add_to_db;
		DEALLOCATE PREPARE p_add_to_db;
		
		# Remove from trash if database was "removed" just before creating
		SET @prep_str=CONCAT('
			DELETE FROM `db_trash_list`
			WHERE `name` = \'', db_name ,'\';
		');
		PREPARE p_add_to_db FROM @prep_str;
		EXECUTE p_add_to_db;
		DEALLOCATE PREPARE p_add_to_db;

		# Prepare new database
		CALL prepare_db(db_name);
	END LOOP;
	
	CLOSE cur;
	
	TRUNCATE TABLE `db_add_list`;
END //

CREATE PROCEDURE alter_database_list()
BEGIN
	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		DECLARE done INT DEFAULT FALSE;
		DECLARE db_name VARCHAR(64);
		DECLARE cur CURSOR FOR
			SELECT `db_add_list`.`name` FROM `db_add_list`
			INNER JOIN `db_list`
			ON `db_add_list`.`name` = `db_list`.`name`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;
		
		OPEN cur;
		
		proc_loop: LOOP
			FETCH cur INTO db_name;
			IF done THEN
				LEAVE proc_loop;
			END IF;
			
			SET @prep_str=CONCAT('
				DROP DATABASE IF EXISTS ', db_name, ';
			');
			PREPARE proc_prep FROM @prep_str;
			EXECUTE proc_prep;
			DEALLOCATE PREPARE proc_prep;
			
			DELETE FROM `db_list`
			WHERE `name` = db_name;
		END LOOP;
		
		CLOSE cur;

		ROLLBACK;

		TRUNCATE TABLE `db_add_list`;
		TRUNCATE TABLE `db_rem_list`;

		RESIGNAL;
	END;
	
	START TRANSACTION;
	
	CALL remove_databases();
	CALL add_databases();
	
	COMMIT;
END //

/*	Protocol for altering database list:
	
	1. call `before_alter_database_list` to create temporary tables if not created;
	2. fill `db_rem_list` and `db_add_list` tables with database names;
	3. call `alter_database_list`
		(or add_databases / remove_databases separately).
	
**********************************************/

/*********************************************
	Alter database schema
*/

CREATE PROCEDURE prepare_alter_proc_for_db(db_name VARCHAR(64))
BEGIN
# Get types count -> @types_count
	SET @prep_str = CONCAT('
		SELECT COUNT(`id`) FROM `', db_name ,'`.`type`
		INTO @types_count;
	');
	PREPARE p_get_types_count FROM @prep_str;
	
# Get type id and name -> @cur_type_id, @cur_type_name
	SET @prep_str = CONCAT('
		SELECT `id`, `name`
		FROM `', db_name ,'`.`type`
		LIMIT ?, 1
		INTO @cur_type_id, @cur_type_name;
	');
	PREPARE p_get_type_id_name FROM @prep_str;
	
# Get type id and table name -> @cur_type_id, @cur_table_name
	SET @prep_str = CONCAT('
		SELECT `id`, `table_name`
		FROM `', db_name ,'`.`type`
		LIMIT ?, 1
		INTO @cur_type_id, @cur_table_name;
	');
	PREPARE p_get_type_id_tablename FROM @prep_str;
	
# Update base type id-s
	SET @prep_str = CONCAT('
		UPDATE `', db_name, '`.`type`
		SET `base_type_id` = ?
		WHERE `base_type_name` = ?;
	');
	PREPARE p_update_base_type FROM @prep_str;
	
# Update owner type id
	SET @prep_str = CONCAT('
		UPDATE `', db_name, '`.`field`
		SET `owner_type_id` = ?
		WHERE `owner_type_name` = ?;
	');
	PREPARE p_update_fields_owner FROM @prep_str;
	
# Update fields ref type id (if ref type)
	SET @prep_str = CONCAT('
		UPDATE `', db_name, '`.`field`
		SET `ref_type_id` = ?
		WHERE `type_name` = ?;
	');
	PREPARE p_update_fields_ref FROM @prep_str;
	
	
# Type-Field optimization table filling
	SET @prep_str = CONCAT('
		INSERT INTO `', db_name, '`.`type_field_map` (`type_id`, `field_id`)
			SELECT ?, `id`
			FROM `', db_name, '`.`field`
			WHERE `owner_type_id` = ?
	');
	PREPARE p_map_base_type_fields FROM @prep_str;
	
# Get base type id to continue filling -> @bi
	SET @prep_str = CONCAT('
		SELECT `base_type_id`
		FROM `', db_name ,'`.`type`
		WHERE `id` = ?
		LIMIT 1
		INTO @bi;
	');
	PREPARE p_next_base_type FROM @prep_str;
	
	
# Get field id, ref_type_id, back_ref_name -> @cur_field_id, @cur_ref_type_id, @cur_back_ref_name
	SET @prep_str = CONCAT('
		SELECT `id`, `ref_type_id`, `back_ref_name`
		FROM `', db_name, '`.`field`
		LIMIT ?, 1
		INTO @cur_field_id, @cur_ref_type_id, @cur_back_ref_name;
	');
	PREPARE p_get_field_id_ref_type_id_back_ref_name FROM @prep_str;
	
# Get field back_ref_id by type and name -> @cur_back_ref_id
	SET @prep_str = CONCAT('
		SELECT `f`.`id`
		FROM `', db_name ,'`.`field` as `f`
		INNER JOIN `', db_name ,'`.`type_field_map` as `m`
			ON `f`.`id` = `m`.`field_id`
			AND `m`.`type_id` = ?
			AND `f`.`name` = ?
		LIMIT 1
		INTO @cur_back_ref_id;
	');
	PREPARE p_get_back_ref_id FROM @prep_str;
	
# Update field back_ref_id
	SET @prep_str = CONCAT('
		UPDATE `', db_name ,'`.`field`
		SET `back_ref_id` = ?
		WHERE `id` = ?;
	');
	PREPARE p_update_field_back_ref_id FROM @prep_str;
	
	
# Get fields count -> @fields_count
	SET @prep_str = CONCAT('
		SELECT COUNT(`id`)
		FROM `', db_name ,'`.`field`
		INTO @fields_count;
	');
	PREPARE p_get_fields_count FROM @prep_str;
	
# Get fields count by type id -> @type_fields_count
	SET @prep_str = CONCAT('
		SELECT COUNT(`field_id`)
		FROM `', db_name ,'`.`type_field_map`
		WHERE `type_id` = ?
		INTO @type_fields_count;
	');
	PREPARE p_get_type_fields_count FROM @prep_str;
	
# Get field info by type id and field number in table
	# -> @cf_col_name, @cf_type_name,
	#    @cf_ref_type_id, @cf_is_list,
	#    @cf_compare_options
	SET @prep_str = CONCAT('
		SELECT `col_name`, `type_name`, `ref_type_id`, `is_list`, `compare_options`
		FROM `', db_name ,'`.`field`
		WHERE `id` = (
			SELECT `field_id`
			FROM `', db_name ,'`.`type_field_map`
			WHERE `type_id` = ?
			LIMIT ?, 1
		)
		LIMIT 1
		INTO @cf_col_name, @cf_type_name, @cf_ref_type_id, @cf_is_list, @cf_compare_options;
	');
	PREPARE p_get_field_info FROM @prep_str;
END //

CREATE PROCEDURE deallocate_alter_proc()
BEGIN
	DEALLOCATE PREPARE p_get_types_count;
	DEALLOCATE PREPARE p_get_type_id_name;
	DEALLOCATE PREPARE p_get_type_id_tablename;
	DEALLOCATE PREPARE p_update_base_type;
	DEALLOCATE PREPARE p_update_fields_owner;
	DEALLOCATE PREPARE p_update_fields_ref;
	
	DEALLOCATE PREPARE p_map_base_type_fields;
	DEALLOCATE PREPARE p_next_base_type;
	
	DEALLOCATE PREPARE p_get_field_id_ref_type_id_back_ref_name;
	DEALLOCATE PREPARE p_get_back_ref_id;
	DEALLOCATE PREPARE p_update_field_back_ref_id;
	
	DEALLOCATE PREPARE p_get_fields_count;
	DEALLOCATE PREPARE p_get_type_fields_count;
	DEALLOCATE PREPARE p_get_field_info;
END //

CREATE PROCEDURE alter_table_for_type(db_name VARCHAR(64), type_no INT)
BEGIN
	DECLARE fields_defs TEXT DEFAULT "";
	DECLARE fields_constraints TEXT DEFAULT "";
	DECLARE field_type VARCHAR(255) DEFAULT "";
    DECLARE nullable_sign_pos INT UNSIGNED DEFAULT 0;
	
	SET @t_no = type_no;
	EXECUTE p_get_type_id_tablename USING @t_no;
	
	EXECUTE p_get_type_fields_count USING @cur_type_id;
	
/* ***** Main cycle on fields ****** */
	
	SET @fi = 0;
	f_loop: LOOP
		IF @fi = @type_fields_count THEN
			LEAVE f_loop;
		END IF;
		
		EXECUTE p_get_field_info USING @cur_type_id, @fi;
		
		IF @cf_ref_type_id IS NULL THEN
			IF NOT @cf_is_list THEN
				SET field_type = @cf_type_name;
				
				IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
# --> Compare Options
					IF @cf_compare_options = 'IgnoreCase' THEN
						SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
					END IF;
				ELSE
# --> Check if nullable
					SELECT LOCATE('?', field_type)
					INTO nullable_sign_pos;
					
					IF nullable_sign_pos = 0 THEN
						SET field_type = CONCAT(field_type, ' NOT NULL');
					ELSE
						SET field_type = SUBSTRING(field_type FROM 1 FOR nullable_sign_pos-1);
					END IF;
				END IF;
				
			ELSE	# <-- @cf_is_list == TRUE
				SET field_type = 'BLOB';
			END IF;
			
		ELSE	# <-- @cf_ref_type_id != NULL
			IF NOT @cf_is_list THEN
				SET field_type = 'BIGINT(0)';
				
				SET fields_constraints = CONCAT(fields_constraints, ',
					FOREIGN KEY (', @cf_col_name,')
						REFERENCES `db_key`(`sys_id`)
						ON DELETE SET NULL
						ON UPDATE SET NULL');
				
			ELSE	# <-- @cf_is_list == TRUE
				SET field_type = 'INT';
				
				SET fields_constraints = CONCAT(fields_constraints, ',
					FOREIGN KEY (', @cf_col_name,')
						REFERENCES `list`(`id`)
						ON DELETE SET NULL');
			END IF;
			
		END IF;
		
		SET fields_defs = CONCAT(fields_defs, ', `', @cf_col_name, '` ', field_type);
		
		SET @fi = @fi + 1;
	END LOOP f_loop;
	
# Create table for type with all fields
	#  (table name can't be a parameter => prepare each time)
	SET @prep_str = CONCAT('
		CREATE TABLE `', db_name ,'`.`', @cur_table_name, '` (
			id BIGINT(0) PRIMARY KEY NOT NULL

			', fields_defs ,',

			FOREIGN KEY (id)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE

				', fields_constraints, '

		) ENGINE=`InnoDB` DEFAULT CHARSET=`utf8` COLLATE `utf8_bin`;
	');
	PREPARE p_create_table FROM @prep_str;
	EXECUTE p_create_table;
	DEALLOCATE PREPARE p_create_table;
END //

CREATE PROCEDURE alter_db_schema(db_name VARCHAR(64))
BEGIN
	CALL prepare_alter_proc_for_db(db_name);
	
/* ***** Cycle on types to set base_type_id, fields' owner_type_id and ref_type_id ****** */
	
	EXECUTE p_get_types_count;	# -> @types_count

	SET @ti = 0;
	typeLoop: LOOP
		IF @ti = @types_count THEN
			LEAVE typeLoop;
		END IF;
		
		EXECUTE p_get_type_id_name USING @ti;	# -> @cur_type_id, @cur_type_name
		
		EXECUTE p_update_base_type USING @cur_type_id, @cur_type_name;
		EXECUTE p_update_fields_owner USING @cur_type_id, @cur_type_name;
		EXECUTE p_update_fields_ref USING @cur_type_id, @cur_type_name;
		
		SET @ti = @ti + 1;
	END LOOP typeLoop;
	
/* ***** 2nd cycle on types to set type-field mapping and fields' ref_id ****** */

	SET @ti = 0;
	typeLoop_2: LOOP
		IF @ti = @types_count THEN
			LEAVE typeLoop_2;
		END IF;
		
		EXECUTE p_get_type_id_name USING @ti;	# -> @cur_type_id, @cur_type_name
		
		SET @bi = @cur_type_id;
		fields_loop: LOOP
			IF @bi IS NULL THEN
				LEAVE fields_loop;
			END IF;
			
			EXECUTE p_map_base_type_fields USING @cur_type_id, @bi;
			EXECUTE p_next_base_type USING @bi;
		END LOOP fields_loop;
		
		SET @ti = @ti + 1;
	END LOOP typeLoop_2;
	
/* ***** Cycle on fields to set back_ref_id ****** */

	EXECUTE p_get_fields_count;	# -> @fields_count
	
	SET @fi = 0;
	fieldsLoop: LOOP
		IF @fi = @fields_count THEN
			LEAVE fieldsLoop;
		END IF;
		
		EXECUTE p_get_field_id_ref_type_id_back_ref_name USING @fi;
		
		IF NOT @cur_ref_type_id IS NULL THEN
			EXECUTE p_get_back_ref_id USING @cur_ref_type_id, @cur_back_ref_name;
			EXECUTE p_update_field_back_ref_id USING @cur_back_ref_id, @cur_field_id;
		END IF;
		
		SET @fi = @fi + 1;
	END LOOP fieldsLoop;
	
	
/* ***** Create table for each type ****** */
	
	SET @ti = 0;
	tables_loop: LOOP
		IF @ti = @types_count THEN
			LEAVE tables_loop;
		END IF;
		
		CALL alter_table_for_type(db_name, @ti);
		
		SET @ti = @ti + 1;
	END LOOP tables_loop;
	
# Set Constraints on fields of all tables here
	#  (prepare and execute agregated string, formed during creating tables)
	
	CALL deallocate_alter_proc();
END //

/*	Protocol for altering database schema:
	
	1. fill `type` and `field` tables with appropriate data;
	2. call `alter_db_schema`

**********************************************/


/*********************************************
	Get table for db_key -> @t_table_name
*/
CREATE PROCEDURE get_type_table(db_name VARCHAR(64), db_key BIGINT(0))
BEGIN
	SET @prep_str = CONCAT('
		SELECT `table_name`
		FROM `', db_name, '`.`type`
		WHERE `id` = (
			SELECT `real_type_id`
			FROM `', db_name, '`.`db_key`
			WHERE `sys_id` = ', db_key, '
			LIMIT 1
		)
		LIMIT 1;
		INTO @t_table_name;
	');
	PREPARE p_get_type_table FROM @prep_str;
	EXECUTE p_get_type_table;
	DEALLOCATE PREPARE p_get_type_table;
END //

/*********************************************
	TODO: other stored procedures...
*/

DELIMITER ;

/******************************************************************************/

/*********************************************

		Create Nezaboodka users
			and grant rights for databases

**********************************************/

CREATE USER `nz_admin`@'%' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'%';

/*	Localhost user	*/
CREATE USER `nz_admin`@'localhost' IDENTIFIED BY  'nezaboodka';
GRANT ALL ON *.* TO `nz_admin`@'localhost';

FLUSH PRIVILEGES;
